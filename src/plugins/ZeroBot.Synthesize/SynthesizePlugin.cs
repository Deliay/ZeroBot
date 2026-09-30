using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
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

        return ValueTask.FromResult(services);
    }
}
