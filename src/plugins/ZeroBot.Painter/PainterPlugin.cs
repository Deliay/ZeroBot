using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Painter;

public class PainterPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();

        services.ConfigureJsonConfig("painter-config.json", PainterOptions.Default, cancellationToken);
        services.AddSingleton<PainterApi>();
        services.AddSingletonComponent<PainterManageCommandHandler>();
        services.AddSingletonComponent<PaintCommandHandler>();

        return ValueTask.FromResult(services);
    }
}
