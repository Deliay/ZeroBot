using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Synthesize.Abstraction;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Synthesize;

public class SynthesizePlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();

        services.ConfigureJsonConfig("synthesize-config.json", SynthesizeOptions.Default, cancellationToken);
        services.AddSingleton<HttpClient>();
        services.AddSingleton<SynthesizeApi>();
        services.AddSingletonComponent<DatasetAliasCommandHandler>();
        services.AddSingletonComponent<SynthesizeCommandHandler>();
        services.AddSingleton<IVoiceBroadcaster, VoiceBroadcastService>();
        services.AddSingletonComponent<VoiceBroadcastCommandHandler>();

        return ValueTask.FromResult(services);
    }
}
