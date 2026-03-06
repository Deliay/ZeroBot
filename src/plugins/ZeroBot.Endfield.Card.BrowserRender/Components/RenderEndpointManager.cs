using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using RouteAttribute = Microsoft.AspNetCore.Components.RouteAttribute;

namespace ZeroBot.Endfield.Card.BrowserRender.Components;

public class RenderEndpointManager
{
    
}

public static class RenderEndpointManagerExtensions
{
    extension(IServiceCollection service)
    {
        public IServiceCollection ConfigureBlazorRender(Assembly? assembly = null)
        {
            assembly ??= Assembly.GetCallingAssembly();
            var components = assembly.DefinedTypes
                .Where(type => type.IsAssignableTo(typeof(ComponentBase)));
            
            foreach (var type in components)
            {
                var route = type.GetCustomAttribute<RouteAttribute>()?.Template;
                if (route is null) continue;

                service.AddTransient(type);
                service.AddTransient(typeof(ComponentBase), type);
                
                service.AddSingleton(new ComponentRouter(route, type));
            }
            
            return service.AddLogging()
                .AddHttpLogging()
                .AddTransient<HtmlRenderer>()
                .AddTransient<BlazorRenderer>();
        }
    }

    extension(WebApplication app)
    {
        public void MapBlazorRendererEndpoints()
        {
            var componentRouters = app.Services.GetServices<ComponentRouter>();
            foreach (var (route, component) in componentRouters)
            {
                app.MapGet($"{route}",
                        async ([FromQuery] string? context, CancellationToken cancellationToken = default) =>
                        {
                            await using var renderer = app.Services.GetRequiredService<BlazorRenderer>();
                            var html = await renderer.RenderAsync(component, context, cancellationToken);
                            return Results.Text(html, "text/html; charset=utf-8", Encoding.UTF8);
                        });
            }
        }
    }
}
