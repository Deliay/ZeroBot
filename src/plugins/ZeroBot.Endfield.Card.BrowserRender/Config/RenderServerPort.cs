using System.ComponentModel.Design;
using System.Net.NetworkInformation;
using ZeroBot.Utility.FileWatcher;
using IComponentInitializer = EmberFramework.Abstraction.Layer.Plugin.IComponentInitializer;

namespace ZeroBot.Endfield.Card.BrowserRender.Config;

public class RenderServerPort(IJsonConfig<RenderServerSettings> config) : IComponentInitializer
{
    public int Port { get; private set; } = 12312;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        await config.WaitForInitializedAsync(cancellationToken);
        Port = config.Current.GetPort();
    }

    public void Dispose()
    {
        
    }

    public ValueTask DisposeAsync()
    {
        return default;
    }
}
