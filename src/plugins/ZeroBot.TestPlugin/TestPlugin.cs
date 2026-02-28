using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.TestPlugin.Components;
using ZeroBot.TestPlugin.Config;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.TestPlugin;

public class TestPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingletonComponent<Ping>();
        services.AddSingletonComponent<Wish>();
        
        services.ConfigureJsonConfig("boat.json", BootsConfig.Default, cancellationToken);
        services.AddSingletonExecutable<Boots>();

        services.AddSingletonComponent<PuzzleSolver>();
        
        return ValueTask.FromResult(services);
    }
}
