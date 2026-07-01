namespace ZeroBot.Endfield.Api.Skland.Authorize;

public class DeviceIdService(IDeviceIdRepository repository)
{
    public async ValueTask<string> GetOrGenerateAsync(string oAuthToken, CancellationToken cancellationToken = default)
    {
        var did = await repository.GetAsync(oAuthToken, cancellationToken);
        if (did is not null)
            return did;

        did = await DeviceIdGenerator.GetDeviceId();
        await repository.SetAsync(oAuthToken, did, cancellationToken);
        return did;
    }
}
