using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Api.Skland;

public static class HypergryphClientExtensions
{
    private static readonly MediaTypeHeaderValue MimeTypeApplicationJson = MediaTypeHeaderValue.Parse("application/json");
    extension(HypergryphClient client)
    {
        public async ValueTask<Response<T>> PostCallAsync<T>(string url, object data, CancellationToken cancellationToken = default)
        {
            var response = await client.CallAsync<T>((req) =>
            {
                req.Method = HttpMethod.Post;
                req.RequestUri = new Uri(url);
                req.Content = JsonContent.Create(data);
                
                foreach (var (key, value) in HypergryphClient.BaseHeaders)
                {
                    req.Headers.TryAddWithoutValidation(key, value);
                }

                req.Headers.TryAddWithoutValidation("User-Agent",
                    "Skland/1.21.0 (com.hypergryph.skland; build:102100065; iOS 17.6.0) Alamofire/5.7.1");
            }, cancellationToken);

            response.EnsureSuccessStatusCode();
            return response;
        }
        
        public async ValueTask<ZonResponse<T>> PostCallZonAsync<T>(string url, object data, string did, CancellationToken cancellationToken = default)
        {
            return await client.CallZonAsync<T>((req) =>
            {
                var json = JsonSerializer.Serialize(data);
                req.Content = new StringContent(json, MimeTypeApplicationJson);
                req.Method = HttpMethod.Post;
                req.RequestUri = new Uri(url);
                
                foreach (var (key, value) in HypergryphClient.BaseHeaders)
                {
                    req.Headers.TryAddWithoutValidation(key, value);
                }

                req.Headers.TryAddWithoutValidation("dId", did);
            }, cancellationToken);
        }

        public async ValueTask<ZonResponse<T>> GetCallZonAsync<T>(string url, UserCredential userCredential,
            CancellationToken cancellationToken = default)
        {
            return await client.CallZonAsync<T>(request: (req) =>
            {
                req.Method = HttpMethod.Get;
                req.RequestUri = new Uri(url);
                var header = HypergryphClient.GetSignedHeaders(url, req.Method, null, userCredential);
                foreach (var (key, value) in header)
                {
                    req.Headers.TryAddWithoutValidation(key, value);
                }
            }, cancellationToken);
        }
        
        public async ValueTask<ZonResponse<T>> PostCallZonAsync<T>(string url, object data, UserCredential userCredential, CancellationToken cancellationToken = default)
        {
            return await client.CallZonAsync<T>(request: (req) =>
            {
                var json = JsonSerializer.Serialize(data);
                req.Method = HttpMethod.Post;
                req.Content = new StringContent(json, MimeTypeApplicationJson);
                req.RequestUri = new Uri(url);
                var header = HypergryphClient.GetSignedHeaders(url, req.Method, json, userCredential);
                foreach (var (key, value) in header)
                {
                    req.Headers.TryAddWithoutValidation(key, value);
                }
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
    }

    extension(HttpResponseMessage message)
    {
        public async ValueTask<Response<T>> ReadHypergryphResponseAsync<T>(CancellationToken cancellationToken = default)
        {
            message.EnsureSuccessStatusCode();
            return (await message.Content.ReadFromJsonAsync<Response<T>>(cancellationToken))!;
        }
        public async ValueTask<ZonResponse<T>> ReadZonResponseAsync<T>(CancellationToken cancellationToken = default)
        {
            message.EnsureSuccessStatusCode();
            return (await message.Content.ReadFromJsonAsync<ZonResponse<T>>(cancellationToken))!;
        }
    }
}