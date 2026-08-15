using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ZeroBot.Bilibili.Dynamic;

namespace ZeroBot.Bilibili.Live;

public class LiveScApi(HttpClient http, VtuberServerOptions options, ILogger<LiveScApi> logger)
{
    private string BaseUrl => options.Endpoint.TrimEnd('/');

    public async Task ReceiveSuperChatEventsAsync(string roomId,
        Func<SuperChatData, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/api/b/live/{roomId}/event/subscribe?type=super_chat";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("text/event-stream");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var data = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;
            if (line.Length == 0)
            {
                await DispatchAsync(data, handler, cancellationToken);
                data.Clear();
                continue;
            }
            if (line.StartsWith("data:")) data.AppendLine(line["data:".Length..].TrimStart());
        }
        if (data.Length > 0) await DispatchAsync(data, handler, cancellationToken);
    }

    private async ValueTask DispatchAsync(StringBuilder data, Func<SuperChatData, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        var json = data.ToString().Trim();
        if (json.Length == 0) return;
        try
        {
            var item = JsonSerializer.Deserialize<LiveEventItem>(json);
            if (item?.Payload?.Data is { } sc) await handler(sc, cancellationToken);
        }
        catch (JsonException e)
        {
            logger.LogWarning(e, "Failed to parse super_chat event: {Json}", json);
        }
    }
}

public class LiveEventItem
{
    [JsonPropertyName("roomId")] public long RoomId { get; set; }

    [JsonPropertyName("type")] public string Type { get; set; } = "";

    [JsonPropertyName("payload")] public LiveEventPayload? Payload { get; set; }
}

public class LiveEventPayload
{
    [JsonPropertyName("cmd")] public string Cmd { get; set; } = "";

    [JsonPropertyName("data")] public SuperChatData? Data { get; set; }
}

public class SuperChatData
{
    [JsonPropertyName("message")] public string Message { get; set; } = "";

    [JsonPropertyName("price")] public int Price { get; set; }

    [JsonPropertyName("user_info")] public SuperChatUser? UserInfo { get; set; }

    [JsonPropertyName("medal_info")] public SuperChatMedal? MedalInfo { get; set; }
}

public class SuperChatUser
{
    [JsonPropertyName("uname")] public string Uname { get; set; } = "";

    [JsonPropertyName("face")] public string Face { get; set; } = "";
}

public class SuperChatMedal
{
    [JsonPropertyName("medal_name")] public string MedalName { get; set; } = "";

    [JsonPropertyName("medal_level")] public int MedalLevel { get; set; }
}
