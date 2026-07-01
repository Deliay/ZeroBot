namespace ZeroBot.Endfield.Config;

public record DeviceIdConfig(Dictionary<string, string> DeviceIds)
{
    public static DeviceIdConfig Empty => new([]);
}
