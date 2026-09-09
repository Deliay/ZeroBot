using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;
using ZeroBot.Weibo.Weibo;

namespace ZeroBot.Weibo;

public class WeiboPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();

        var endpoint = Environment.GetEnvironmentVariable("Z_VTUBER_SERVER_ENDPOINT")
                       ?? "http://vtuber.internal.fffdan.com";
        services.AddSingleton(new VtuberServerOptions(endpoint));
        services.AddSingleton<HttpClient>();
        services.ConfigureJsonConfig("weibo-config.json", WeiboOptions.Default, cancellationToken);

        services.AddSingleton<WeiboApi>();
        services.AddSingletonComponent<WeiboCommandHandler>();
        services.AddSingletonExecutable<WeiboSubscriber>();
        return ValueTask.FromResult(services);
    }
}