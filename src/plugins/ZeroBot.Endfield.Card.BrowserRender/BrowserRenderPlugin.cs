using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Endfield.Card.BrowserRender.Abstraction;
using ZeroBot.Endfield.Card.BrowserRender.Config;
using ZeroBot.Endfield.Card.BrowserRender.Service;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Endfield.Card.BrowserRender;

public class BrowserRenderPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = new CancellationToken())
    {
        IServiceCollection services = new ServiceCollection();
        services.ConfigureJsonConfig("render.json", RenderServerSettings.Empty, cancellationToken);
        services.AddSingletonComponent<RenderServerPort>();
        services.AddSingletonExecutable<RenderApiServer>();
        services.AddSingleton<IRenderContextProvider, RenderContextProvider>();

        return ValueTask.FromResult(services);
    }
}