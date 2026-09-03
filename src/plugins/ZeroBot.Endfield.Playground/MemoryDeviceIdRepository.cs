using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Playground;

public class MemoryDeviceIdRepository : IDeviceIdRepository
{
    private readonly Dictionary<string, string> _deviceIds = new();
    public ValueTask<string?> GetAsync(string oAuthToken, CancellationToken cancellationToken = default)
    {
        _deviceIds.TryGetValue(oAuthToken, out var deviceId);
        
        return ValueTask.FromResult(deviceId);
    }

    public ValueTask SetAsync(string oAuthToken, string deviceId, CancellationToken cancellationToken = default)
    {
        _deviceIds.Add(oAuthToken, deviceId);
        return default;
    }
}