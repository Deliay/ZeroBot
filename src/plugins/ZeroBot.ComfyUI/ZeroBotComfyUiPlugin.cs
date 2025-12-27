using ComfySharpSDK;
using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ZeroBot.ComfyUI;

public class ZeroBotComfyUiPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = new CancellationToken())
    {
        IServiceCollection service = new ServiceCollection();

        service.AddSingleton<ComfyClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ComfyUiOptions>>();
            var config = options.Value;

            return new ComfyClient(config.ComfyUiEndpoint, maxAttempts: 1);
        });
        service.AddSingleton<IComponentInitializer, ToAkumaria>();
        
        return ValueTask.FromResult(service);
    }
}