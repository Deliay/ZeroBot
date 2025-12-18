using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Permission.Abstraction;

namespace ZeroBot.Permission;

public class ZeroBotPermissionPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = new CancellationToken())
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<PermissionManager>();
        services.AddSingleton<IPermissionManager, PermissionManager>(sp => sp.GetRequiredService<PermissionManager>());
        services.AddSingleton<IComponentInitializer>(sp => sp.GetRequiredService<PermissionManager>());
        return ValueTask.FromResult(services);
    }
}
