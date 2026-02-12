using System.Net.Http.Headers;
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
        DefaultRequestHeaders.Add("Content-Type", "application/json");
    }
}