using System.Text.Json;

namespace ZeroBot.Endfield.Api.Skland.Authorize;

public class DeviceIdService : IDeviceIdProvider
{
    private const string FilePath = "device_id.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private Dictionary<string, string>? _cache;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async ValueTask<string> GetDeviceIdAsync(string oAuthToken, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _cache ??= await LoadAsync(cancellationToken);
            if (_cache.TryGetValue(oAuthToken, out var did))
                return did;
        }
        finally
        {
            _lock.Release();
        }

        var newDid = await DeviceIdGenerator.GetDeviceId();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _cache[oAuthToken] = newDid;
            await SaveAsync(_cache, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        return newDid;
    }

    private static async Task<Dictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, cancellationToken);
        var config = JsonSerializer.Deserialize<DeviceIdConfig>(json);
        return config?.DeviceIds ?? [];
    }

    private static async Task SaveAsync(Dictionary<string, string> dict, CancellationToken cancellationToken)
    {
        var config = new DeviceIdConfig(dict);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(FilePath, json, cancellationToken);
    }
}
