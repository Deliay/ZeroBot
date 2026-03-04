using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Api.Skland;

public static class HypergryphClientExtensions
{
    private static readonly MediaTypeHeaderValue MimeTypeApplicationJson =
        MediaTypeHeaderValue.Parse("application/json");

    private const string UserAgentApp =
        "Skland/1.21.0 (com.hypergryph.skland; build:102100065; iOS 17.6.0) Alamofire/5.7.1";
    
    private const string UserAgentWeb =
        "Mozilla/5.0 (Linux; Android 12; SM-A5560 Build/V417IR; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/101.0.4951.61 Safari/537.36; SKLand/1.52.1";

    private static readonly IReadOnlyDictionary<string, string> BaseHeaders = new Dictionary<string, string>
    {
        { "Accept-Encoding", "gzip" },
        { "Connection", "close" },
        { "X-Requested-With", "com.hypergryph.skland" },
    };

    private static readonly JsonSerializerOptions SignSerializerOptions =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static IReadOnlyDictionary<string, string> GenerateSignature(
        UserCredential credential, string path, string bodyOrQuery)
    {
        var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        var headerCa = new Dictionary<string, string>
        {
            { "platform", "3" },
            { "timestamp", timestamp },
            { "dId", credential.DeviceId },
            { "vName", "1.0.0" },
        };
        var headerCaStr = JsonSerializer.Serialize(headerCa, SignSerializerOptions);

        var s = $"{path}{bodyOrQuery}{timestamp}{headerCaStr}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(credential.RefreshToken));
        var hmacResult = hmac.ComputeHash(Encoding.UTF8.GetBytes(s));
        var hmacHex = BitConverter.ToString(hmacResult).Replace("-", "").ToLower();

        using var md5 = MD5.Create();
        var sign = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(hmacHex))).Replace("-", "")
            .ToLower();

        return new Dictionary<string, string>(BaseHeaders)
        {
            { "cred", credential.Cred },
            { "sign", sign },
            { "platform", "3" },
            { "timestamp", $"{timestamp}" },
            { "dId", credential.DeviceId },
            { "vName", "1.0.0" },
        };
    }

    public static async ValueTask<IReadOnlyDictionary<string, string>> GetSignedHeaders(
        HttpRequestMessage req, UserCredential credential, CancellationToken cancellationToken = default)
    {
        var uri = req.RequestUri!;
        var path = uri.AbsolutePath;
        var query = uri.Query.TrimStart('?');

        if (req.Method == HttpMethod.Get) return GenerateSignature(credential, path, query);
        if (req.Content is not { } body) return GenerateSignature(credential, path, "");

        var bodyString = await body.ReadAsStringAsync(cancellationToken);
        
        return GenerateSignature(credential, path, bodyString);
    }

    extension(HypergryphClient client)
    {
        public async ValueTask<Response<T>> PostCallAsync<T>(string url, object data,
            CancellationToken cancellationToken = default)
        {
            var response = await client.CallAsync<T>((req) =>
            {
                req.Method = HttpMethod.Post;
                req.RequestUri = new Uri(url);
                req.Content = JsonContent.Create(data);

                req.FillUserAgentApp();
                req.FillBaseHeaders();
            }, cancellationToken);

            response.EnsureSuccessStatusCode();
            return response;
        }

        public async ValueTask<ZonResponse<T>> PostCallZonAsync<T>(string url, object data, string did,
            CancellationToken cancellationToken = default)
        {
            return await client.CallZonAsync<T>((req) =>
            {
                var json = JsonSerializer.Serialize(data);
                req.Content = new StringContent(json, MimeTypeApplicationJson);
                req.Method = HttpMethod.Post;
                req.RequestUri = new Uri(url);

                req.FillUserAgentApp();
                req.FillBaseHeaders();
                req.FillDIdHeader(did);
            }, cancellationToken);
        }

        public async ValueTask<ZonResponse<T>> GetCallZonAsync<T>(string url, UserCredential userCredential,
            CancellationToken cancellationToken = default)
        {
            return await client.CallZonAsync<T>(request: async (req) =>
            {
                req.Method = HttpMethod.Get;
                req.RequestUri = new Uri(url);
                req.FillUserAgentApp();
                await req.FillSignedRequestAsync(userCredential, cancellationToken);
            }, cancellationToken);
        }

        public async ValueTask<ZonResponse<T>> PostCallZonAsync<T>(string url, object data,
            UserCredential userCredential, CancellationToken cancellationToken = default)
        {
            return await client.CallZonAsync<T>(request: async (req) =>
            {
                var json = JsonSerializer.Serialize(data);
                req.Method = HttpMethod.Post;
                req.Content = new StringContent(json, MimeTypeApplicationJson);
                req.RequestUri = new Uri(url);
                req.FillUserAgentWeb();
                await req.FillSignedRequestAsync(userCredential, cancellationToken);
            }, cancellationToken);
        }

        public async ValueTask<Response<T>> CallAsync<T>(
            Action<HttpRequestMessage> request,
            CancellationToken cancellationToken = default)
        {
            var req = new HttpRequestMessage();
            request(req);
            var response = await client.SendAsync(req, cancellationToken);
            return await response.ReadHypergryphResponseAsync<T>(cancellationToken);
        }

        public async ValueTask<ZonResponse<T>> CallZonAsync<T>(
            Action<HttpRequestMessage> request,
            CancellationToken cancellationToken = default)
        {
            var req = new HttpRequestMessage();
            request(req);
            var response = await client.SendAsync(req, cancellationToken);
            return await response.ReadZonResponseAsync<T>(cancellationToken);
        }

        public async ValueTask<ZonResponse<T>> CallZonAsync<T>(
            Func<HttpRequestMessage, ValueTask> request,
            CancellationToken cancellationToken = default)
        {
            var req = new HttpRequestMessage();
            await request(req);
            var response = await client.SendAsync(req, cancellationToken);
            return await response.ReadZonResponseAsync<T>(cancellationToken);
        }
    }
    
    extension(HttpRequestMessage req)
    {
        public HttpRequestMessage FillUserAgentWeb()
        {
            return req.FillUserAgent(UserAgentWeb);
        }
        public HttpRequestMessage FillUserAgentApp()
        {
            return req.FillUserAgent(UserAgentApp);
        }

        public HttpRequestMessage FillUserAgent(string userAgent)
        {
            req.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            return req;
        }
        
        public HttpRequestMessage FillHeaders(IReadOnlyDictionary<string, string> headers)
        {
            foreach (var (key, value) in headers)
            {
                req.Headers.TryAddWithoutValidation(key, value);
            }

            return req;
        }
        
        public HttpRequestMessage FillBaseHeaders()
        {

            return req.FillHeaders(BaseHeaders);
        }

        public HttpRequestMessage FillDIdHeader(string did)
        {
            req.FillBaseHeaders();
            req.Headers.TryAddWithoutValidation("dId", did);
            return req;
        }

        public async ValueTask FillSignedRequestAsync(UserCredential credential, CancellationToken cancellationToken = default)
        {
            req.FillHeaders(await GetSignedHeaders(req, credential, cancellationToken));
        }
    }

    extension(HttpResponseMessage message)
    {
        public async ValueTask<Response<T>> ReadHypergryphResponseAsync<T>(
            CancellationToken cancellationToken = default)
        {
            message.EnsureSuccessStatusCode();
            return await message.Content.ReadFromJsonAsync<Response<T>>(cancellationToken);
        }

        public async ValueTask<ZonResponse<T>> ReadZonResponseAsync<T>(CancellationToken cancellationToken = default)
        {
            message.EnsureSuccessStatusCode();
            return await message.Content.ReadFromJsonAsync<ZonResponse<T>>(cancellationToken);
        }
    }
}