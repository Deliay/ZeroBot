using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Utility;

namespace ZeroBot.TestPlugin;

public class TestPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = new CancellationToken())
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingletonComponent<Ping>();
        services.AddSingletonComponent<Wish>();
        
        return ValueTask.FromResult(services);
    }
}
