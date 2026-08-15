using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Mikibot.Crawler;
using Mikibot.Crawler.Http.Bilibili;
using ZeroBot.Bilibili.Dynamic;
using ZeroBot.Bilibili.Live;
using ZeroBot.Bilibili.Video;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Bilibili;

public class BiliBiliPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<HttpClient>();
        services.AddBilibiliCrawlers(addHttpClient: false);
        services.AddSingleton<BiliVideoCrawler>();
        var vtuberServerEndpoint = Environment.GetEnvironmentVariable("Z_VTUBER_SERVER_ENDPOINT")
                                   ?? "http://vtuber.internal.fffdan.com";
        services.AddSingleton(new VtuberServerOptions(vtuberServerEndpoint));
        services.AddSingleton<VtuberSpaceApi>();
        services.ConfigureJsonConfig("bilibili-config.json", BilibiliOptions.Default, cancellationToken);
        services.AddSingletonComponent<LiveStatutCommandHandler>();
        services.AddSingletonComponent<DynamicCommandHandler>();
        services.AddSingletonExecutable<VideoLinkParser>();
        services.AddSingletonExecutable<LiveStatusSubscriber>();
        services.AddSingletonExecutable<DynamicSubscriber>();
        
        return ValueTask.FromResult(services);
    }
}
