using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ZeroBot.Endfield.Card.BrowserRender.Abstraction;

namespace ZeroBot.Endfield.Card.BrowserRender.Components;

public class BlazorRenderer(
    HtmlRenderer renderer,
    IRenderContextProvider contextProvider): IDisposable, IAsyncDisposable
{
    public async ValueTask<string> RenderAsync(Type template, string? context, CancellationToken cancellationToken = default)
    {
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            if (!contextProvider.TryGetValue(context, out var contextValue))
            {
                return (await renderer.RenderComponentAsync(template)).ToHtmlString();
            }

            var parameter = ParameterView.FromDictionary(contextValue);
            return (await renderer.RenderComponentAsync(template, parameter)).ToHtmlString();
        });
    }

    public void Dispose()
    {
        renderer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await renderer.DisposeAsync();
    }
}