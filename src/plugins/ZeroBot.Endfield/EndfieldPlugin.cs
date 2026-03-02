using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Endfield.Component;
using ZeroBot.Endfield.Config;
using ZeroBot.Endfield.Api.Skland.Sign;
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

        services.AddSingleton<DeviceIdManager>();
        services.AddSingleton<SklandSignHelper>();

        return ValueTask.FromResult(services);
    }
}
