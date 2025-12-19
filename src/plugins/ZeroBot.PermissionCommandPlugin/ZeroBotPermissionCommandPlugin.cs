using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace ZeroBot.PermissionCommandPlugin;

public class ZeroBotPermissionCommandPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = new CancellationToken())
    {
        IServiceCollection services = new ServiceCollection();

        services.AddSingleton<IComponentInitializer, PermissionManagerCommand>();

        return ValueTask.FromResult(services);
    }
}