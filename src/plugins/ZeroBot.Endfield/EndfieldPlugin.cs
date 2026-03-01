using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Endfield.Component;
using ZeroBot.Endfield.Config;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Endfield;

public class EndfieldPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = new CancellationToken())
    {
        IServiceCollection services = new ServiceCollection();
        services.ConfigureJsonConfig("puzzle.json", PuzzleSolverConfig.Default, cancellationToken);
        services.AddSingletonComponent<PuzzleSolver>();

        return ValueTask.FromResult(services);
    }
}
