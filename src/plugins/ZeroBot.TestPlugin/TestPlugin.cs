using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace ZeroBot.TestPlugin;

public class TestPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = new CancellationToken())
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IComponentInitializer, PingCommand>();
        
        return ValueTask.FromResult(services);
    }
}