using EmberFramework.Abstraction;
using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Milky.Net.Client;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Milky.Bot;
using ZeroBot.Milky.Configuration;

namespace ZeroBot.Milky;

public class MilkyPlugin(IConfiguration config) : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = new CancellationToken())
    {
        IServiceCollection services = new ServiceCollection();
        services.AddMemoryCache();
        services.Configure<MilkyOptions>(config.GetSection(nameof(MilkyOptions)));
        services.AddSingleton<MilkyHttpClient>();
        services.AddSingleton<HttpClient>(s => s.GetRequiredService<MilkyHttpClient>());
        services.AddSingleton<MilkyWebSocketReceiver>();
        services.AddSingleton<IBotService, MilkyBot>();
        services.AddSingleton<BotInfos>();
        services.AddSingleton<IExecutable>(s => s.GetRequiredService<IBotService>());
        services.AddSingleton(sp =>
        {
            var milkyHttpClient = sp.GetRequiredService<MilkyHttpClient>();
            return new MilkyClient(milkyHttpClient);
        });

        return ValueTask.FromResult(services);
    }
}