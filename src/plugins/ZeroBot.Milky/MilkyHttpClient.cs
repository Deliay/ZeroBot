using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;
using ZeroBot.Milky.Configuration;

namespace ZeroBot.Milky;

public class MilkyHttpClient : HttpClient
{
    public MilkyHttpClient(IOptions<MilkyOptions> config)
    {
        BaseAddress =
            config.Value.MilkyServer ?? throw new InvalidOperationException("Milky server is not configured.");
        DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            config.Value.AccessToken ??
            throw new InvalidOperationException("Milky access token is not configured."));
    }

    private static void HandleMessageContentType(HttpRequestMessage request)
    {
        // fixed content-type for LLBot failure when request does not include body
        request.Content ??= new StringContent("", Encoding.UTF8, "application/json");
        // fixed content-type headers to "application/json" while LLBot will fail when
        // content-type includes charset
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    }
    
    public override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HandleMessageContentType(request);
        return base.Send(request, cancellationToken);
    }

    public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HandleMessageContentType(request);
        return base.SendAsync(request, cancellationToken);
    }
}