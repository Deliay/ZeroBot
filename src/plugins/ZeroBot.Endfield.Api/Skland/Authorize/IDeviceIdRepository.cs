namespace ZeroBot.Endfield.Api.Skland.Authorize;

public interface IDeviceIdRepository
{
    ValueTask<string?> GetAsync(string oAuthToken, CancellationToken cancellationToken = default);
    ValueTask SetAsync(string oAuthToken, string deviceId, CancellationToken cancellationToken = default);
}
