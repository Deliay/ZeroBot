using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Utility;

namespace ZeroBot.PermissionCommandPlugin;

public class ZeroBotPermissionCommandPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = new CancellationToken())
    {
        IServiceCollection services = new ServiceCollection();

        services.AddSingletonComponent<PermissionManagerCommand>();

        return ValueTask.FromResult(services);
    }
}
