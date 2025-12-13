using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Milky.Net.Client;
using ZeroBot.Milky.Bot;
using ZeroBot.Milky.Configuration;

namespace ZeroBot.Milky;

public class MilkyPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = new CancellationToken())
    {
        IServiceCollection services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton<MilkyHttpClient>();
        services.AddSingleton<MilkyWebSocketReceiver>();
        services.AddSingleton<MilkyBot>();
        services.AddSingleton((sp) => new MilkyClient(sp.GetRequiredService<MilkyHttpClient>()));
        services.AddSingleton<BotInfos>();

        return default;
    }
}