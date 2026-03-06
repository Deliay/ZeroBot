using System.Net;
using System.Net.NetworkInformation;

namespace ZeroBot.Endfield.Card.BrowserRender.Config;

public record RenderServerSettings(int? CustomPort = null, bool UseRandomPort = true)
{
    public static RenderServerSettings Empty { get; } = new();
}

public static class RenderServerSettingsExtensions
{
    public static int Random()
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var openedPorts = properties
            .GetActiveTcpListeners()
            .Concat(properties.GetActiveUdpListeners())
            .Select(item => item.Port)
            .ToHashSet();
        return Enumerable
            .Range(10000, 40000)
            .Except(openedPorts)
            .Shuffle()
            .First();
    }

    extension(RenderServerSettings settings)
    {
        public int GetPort()
        {
            return settings is { UseRandomPort: false, CustomPort: not null }
                ? settings.CustomPort.Value
                : Random();
        }
    }
}
