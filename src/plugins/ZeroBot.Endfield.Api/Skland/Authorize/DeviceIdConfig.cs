using System.Text.Json.Serialization;

namespace ZeroBot.Endfield.Api.Skland.Authorize;

public record DeviceIdConfig([property: JsonPropertyName("deviceIds")] Dictionary<string, string> DeviceIds)
{
    public static DeviceIdConfig Empty => new([]);
}
