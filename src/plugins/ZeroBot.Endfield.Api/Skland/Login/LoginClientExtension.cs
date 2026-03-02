using System.Net.Http.Headers;
using System.Net.Http.Json;
using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Api.Skland.Login;

public static class LoginClientExtension
{
    private static readonly HttpContent LoginBody = new StringContent("{\"appCode\":\"4ca99fa6b56cc2ba\"}",
        new MediaTypeHeaderValue("application/json"));

    extension(HypergryphClient client)
    {
        public async ValueTask<LoginQrCodeResponse> GenerateLoginQrCode(CancellationToken cancellationToken = default)
        {
            var result = await client.PostCallAsync<LoginQrCodeResponse>(
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
            var response = await client.GetFromJsonAsync<Response<LoginScanStatusResponse>>(url, cancellationToken);
            return response;
        }

        public async ValueTask<ScanTokenToOAuthTokenResponse> GetOAuthTokenByScanCode(
            string scanCode,
            CancellationToken cancellationToken = default)
        {
            var response = await client.PostCallAsync<ScanTokenToOAuthTokenResponse>(
                "https://as.hypergryph.com/user/auth/v1/token_by_scan_code",
                new ScanTokenToOAuthTokenRequest(scanCode),
                cancellationToken);

            response.EnsureSuccessStatusCode();
            return response.data;
        }

        public async Task<string> GrantAuthorizationCodeAsync(string oAuthToken,
            CancellationToken cancellationToken = default)
        {
            var result = await client.PostCallAsync<OAuthGrantResponse>(
                "https://as.hypergryph.com/user/oauth2/v2/grant",
                new OAuthGrantRequest(oAuthToken),
                cancellationToken);

            result.EnsureSuccessStatusCode();
            return result.data.code;
        }

        private async Task<Credential> GenerateCredentialAsync(string authorization,
            CancellationToken cancellationToken = default)
        {
            var did = await DeviceIdGenerator.GetDeviceId();
            var result = await client.PostCallAsync<CredentialResponse>(
                "https://zonai.skland.com/web/v1/user/auth/generate_cred_by_code",
                new CredentialRequest(authorization),
                did,
                cancellationToken
            );

            result.EnsureSuccessStatusCode();
            return new Credential
            {
                Token = result.data.token,
                Cred = result.data.cred,
                DeviceId = did,
            };
        }
    }
}