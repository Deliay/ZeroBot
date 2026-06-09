using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Endfield.Api.Extension;
using ZeroBot.Endfield.Component;
using ZeroBot.Endfield.Config;
using ZeroBot.Endfield.Credential.Json;
using ZeroBot.Endfield.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Endfield;

public class EndfieldPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();
        services.ConfigureJsonConfig("puzzle.json", PuzzleSolverConfig.Default, cancellationToken);
        services.AddSingletonComponent<PuzzleSolver>();

        services.ConfigureJsonConfig("sign_settings.json", SklandDailySignConfig.Empty, cancellationToken);
        services.AddEndfieldApi(_ => new JsonCredentialRepository("sign_credentials.json"));
        services.AddSingletonComponent<ScanQrCodeTaskManager>();
        services.AddSingletonExecutable<DailySignPeriodicTask>();
        services.AddSingleton<SklandService>();
        
        services.AddSingletonComponent<HypergraphyCommand>();
        services.AddSingleton<BindingCommandHandlers>();
        services.AddSingleton<EndfieldCommandHandlers>();

        services.ConfigureJsonConfig("endfield_version_subscription.json",
            EndfieldVersionSubscriptionConfig.Empty, cancellationToken);
        services.AddSingletonExecutable<EndfieldVersionPollingTask>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<EndfieldVersionSubscriptionCommand>();

        services.ConfigureJsonConfig("endfield_server_status_subscription.json",
            EndfieldServerStatusSubscriptionConfig.Empty, cancellationToken);
        services.AddSingletonExecutable<EndfieldServerStatusPollingTask>();
        services.AddSingleton<EndfieldServerStatusSubscriptionCommand>();

        services.AddMemoryCache();
        return ValueTask.FromResult(services);
    }
}
