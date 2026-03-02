using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Polly;
using Polly.Retry;
using ZeroBot.Endfield.Api.Skland.Login;
using ZeroBot.Endfield.Api.Skland.Sign;

namespace ZeroBot.Endfield.Api.Skland;

public class HypergryphClient(DeviceIdManager deviceIdManager, int maxRetries = 3) : HttpClient
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly ResiliencePipeline _retry = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions() { MaxRetryAttempts = maxRetries }).Build();

    private static readonly HttpContent LoginBody = new StringContent("{\"appCode\":\"4ca99fa6b56cc2ba\"}",
        new MediaTypeHeaderValue("application/json"));
    
    public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var did = await deviceIdManager.GenerateDeviceId(cancellationToken);
        foreach (var header in GetBaseHeaders(did))
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        
        return (await _retry.ExecuteAsync(async (token) => await base.SendAsync(request, token), cancellationToken))!;
    }

    private static Dictionary<string, string> GetBaseHeaders(string did)
    {
        return new Dictionary<string, string>
        {
            {"User-Agent", SklandConstants.UserAgent},
            {"Accept-Encoding", "gzip"},
            {"Connection", "close"},
            {"X-Requested-With", "com.hypergryph.skland"},
            {"dId", did},
        };
    }
    
    public async ValueTask<LoginQrCodeResponse> GenerateLoginQrCode(CancellationToken cancellationToken = default)
    {
        var result = await this.CallAsync<LoginQrCodeResponse>(
            "https://as.hypergryph.com/general/v1/gen_scan/login", 
            LoginBody,
            cancellationToken);

        result.EnsureSuccessStatusCode();
        return result.data;
    }

    public async ValueTask<Response<LoginScanStatusResponse>> GetLoginQrCodeStatus(string scanId,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://as.hypergryph.com/general/v1/scan_status?scanId={scanId}";
        var response = await this.GetFromJsonAsync<Response<LoginScanStatusResponse>>(url, cancellationToken);
        return response;
    }

    public async ValueTask<ScanTokenToOAuthTokenResponse> GetOAuthTokenByScanCode(
        string scanCode,
        CancellationToken cancellationToken = default)
    {
        var response = await this.CallAsync<ScanTokenToOAuthTokenResponse>(
            "https://as.hypergryph.com/user/auth/v1/token_by_scan_code",
            new ScanTokenToOAuthTokenRequest(scanCode),
            cancellationToken);
        
        response.EnsureSuccessStatusCode();
        return response.data;
    }
    
    public async Task<string> GrantAuthorizationCodeAsync(string oAuthToken, CancellationToken cancellationToken = default)
    {
        var result = await this.CallAsync<OAuthGrantResponse>(
            "https://as.hypergryph.com/user/oauth2/v2/grant",
            new OAuthGrantRequest(oAuthToken),
            cancellationToken);
        
        result.EnsureSuccessStatusCode();
        return result.data.code;
    }

    private async Task<Credential> GenerateCredentialAsync(string authorization, CancellationToken cancellationToken = default)
    {
        var result = await this.CallAsync<CredentialResponse>(
            "https://zonai.skland.com/web/v1/user/auth/generate_cred_by_code",
            new CredentialRequest(authorization),
            cancellationToken
        );

        result.EnsureSuccessStatusCode();
        return new Credential
        {
            Token = result.data.token,
            Cred = result.data.cred,
        };
    }
    
    
    private static Dictionary<string, string> GetSignedHeaders(string url, HttpMethod method, string? body, Credential cred, string did)
    {
        var uri = new Uri(url);
        var path = uri.AbsolutePath;
        var query = uri.Query.TrimStart('?');

        string sign;
        Dictionary<string, string> headerCa;

        if (method == HttpMethod.Get)
        {
            (sign, headerCa) = SklandEncryption.GenerateSignature(cred.Token, path, query, did);
        }
        else
        {
            (sign, headerCa) = SklandEncryption.GenerateSignature(cred.Token, path, body ?? "", did);
        }

        var headers = GetBaseHeaders(did);
        headers["cred"] = cred.Cred;
        headers["sign"] = sign;
        foreach (var entry in headerCa)
        {
            headers[entry.Key] = entry.Value;
        }

        return headers;
    }
}