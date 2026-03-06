using System.Reflection;
using EmberFramework.Abstraction;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleHttpServer.Host;
using SimpleHttpServer.Pipeline.Middlewares;
using ZeroBot.Endfield.Card.BrowserRender.Abstraction;
using ZeroBot.Endfield.Card.BrowserRender.Components;
using ZeroBot.Endfield.Card.BrowserRender.Config;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Endfield.Card.BrowserRender;

public class RenderApiServer(
    IJsonConfig<RenderServerSettings> config,
    RenderServerPort port,
    IRenderContextProvider contextProvider,
    ILogger<RenderApiServer> logger) : IExecutable
{
    private async ValueTask RunCoreAsync(CancellationToken cancellationToken = default)
    {
        await config.WaitForInitializedAsync(cancellationToken);
        var rootPath = Path.Combine(Environment.CurrentDirectory, "wwwroot");
        if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions()
        {
            ApplicationName = typeof(RenderApiServer).Assembly.GetName().Name,
            ContentRootPath = Path.GetDirectoryName(typeof(RenderApiServer).Assembly.Location),
            WebRootPath = rootPath,
        });
        builder.WebHost.ConfigureKestrel((_, server) =>
        {
            server.Configure().LocalhostEndpoint(port.Port);
        });
        builder.Services.ConfigureBlazorRender();
        builder.Services.AddSingleton(contextProvider);
        builder.Services.AddServerSideBlazor();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        var app = builder.Build();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.UseStaticFiles();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
        await app.RunAsync(cancellationToken);
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            await RunCoreAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception thrown when starting server!");
        }
    }
}