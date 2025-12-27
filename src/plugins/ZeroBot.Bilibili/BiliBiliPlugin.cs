using EmberFramework.Abstraction;
using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Mikibot.Crawler.Http.Bilibili;
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
        services.AddSingleton<BiliVideoCrawler>();
        services.AddSingleton<BiliLiveCrawler>();
        services.ConfigureJsonConfig("bilibili-config.json", BilibiliOptions.Default, cancellationToken);
        services.AddSingletonComponent<VideoLinkParser>();
        services.AddSingletonComponent<LiveStatutCommandHandler>();
        services.AddSingletonExecutable<LiveStatusSubscriber>();
        
        return ValueTask.FromResult(services);
    }
}
