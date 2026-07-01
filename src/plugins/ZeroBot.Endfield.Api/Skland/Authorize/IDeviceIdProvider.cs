namespace ZeroBot.Endfield.Api.Skland.Authorize;

public interface IDeviceIdProvider
{
    ValueTask<string> GetDeviceIdAsync(string oAuthToken, CancellationToken cancellationToken = default);
}
