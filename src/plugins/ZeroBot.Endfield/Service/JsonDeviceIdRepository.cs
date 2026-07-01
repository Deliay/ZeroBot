using ZeroBot.Endfield.Api.Skland.Authorize;
using ZeroBot.Endfield.Config;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Endfield.Service;

public class JsonDeviceIdRepository(IJsonConfig<DeviceIdConfig> config) : IDeviceIdRepository
{
    public async ValueTask<string?> GetAsync(string oAuthToken, CancellationToken cancellationToken = default)
    {
        await config.WaitForInitializedAsync(cancellationToken);
        return config.Current.DeviceIds.TryGetValue(oAuthToken, out var did) ? did : null;
    }

    public async ValueTask SetAsync(string oAuthToken, string deviceId, CancellationToken cancellationToken = default)
    {
        await config.BeginConfigMutationScopeAsync((cfg, token) =>
        {
            cfg.DeviceIds[oAuthToken] = deviceId;
            return config.SaveAsync(cfg, token);
        }, cancellationToken);
    }
}
