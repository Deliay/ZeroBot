using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Abstraction.Service;
using ZeroBot.Synthesize.Abstraction;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Synthesize;

public class SynthesizePlugin : IPlugin.IWithInitializer, IDisposable
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();

        services.ConfigureJsonConfig("synthesize-config.json", SynthesizeOptions.Default, cancellationToken);
        services.AddSingleton<HttpClient>();
        services.AddSingleton<SynthesizeApi>();
        services.AddSingletonComponent<DatasetAliasCommandHandler>();
        services.AddSingletonComponent<SynthesizeCommandHandler>();
        services.AddSingleton<VoiceBroadcastService>();
        services.AddSingleton<IVoiceBroadcaster>(sp => sp.GetRequiredService<VoiceBroadcastService>());
        services.AddSingletonComponent<VoiceBroadcastCommandHandler>();

        return ValueTask.FromResult(services);
    }

    public void Dispose()
    {
        _serviceRegistration?.Dispose();
    }

    private Registration? _serviceRegistration;
    public ValueTask InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var svc = services.GetRequiredService<IServiceManager>();
        var broadcast = services.GetRequiredService<VoiceBroadcastService>();
        svc.TryRegister<IVoiceBroadcaster>(broadcast, out _serviceRegistration);

        return ValueTask.CompletedTask;
    }
}
