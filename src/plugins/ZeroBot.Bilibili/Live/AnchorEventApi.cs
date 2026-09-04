using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Mikibot.Crawler.WebsocketCrawler.Data.Commands.KnownCommand;
using Mikibot.Crawler.WebsocketCrawler.Data.Commands.KnownCommand.ProtoCommand;
using ZeroBot.Bilibili.Dynamic;

namespace ZeroBot.Bilibili.Live;

public class AnchorEventApi(HttpClient http, VtuberServerOptions options, ILogger<AnchorEventApi> logger)
{
    private string BaseUrl => options.Endpoint.TrimEnd('/');

    public async Task<VtuberUserInfo?> GetUserAsync(string mid, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{BaseUrl}/api/b/user/{mid}";
            var response = await http.GetAsync(url, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<VtuberUserInfo>(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to get user info for mid: {Mid}", mid);
            return null;
        }
    }

    public async Task ReceiveDanmakuEventsAsync(string roomId,
        Func<DanmuMsg, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/api/b/live/{roomId}/event/subscribe?type=danmaku";
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
                await DispatchDanmakuAsync(data, handler, cancellationToken);
                data.Clear();
                continue;
            }
            if (line.StartsWith("data:")) data.AppendLine(line["data:".Length..].TrimStart());
        }
        if (data.Length > 0) await DispatchDanmakuAsync(data, handler, cancellationToken);
    }

    private async ValueTask DispatchDanmakuAsync(StringBuilder data, Func<DanmuMsg, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        var json = data.ToString().Trim();
        if (json.Length == 0) return;
        try
        {
            var item = JsonSerializer.Deserialize<DanmakuEventItem>(json);
            if (item?.Payload is { Cmd: "DANMU_MSG", Info: { } info })
                await handler(info, cancellationToken);
        }
        catch (JsonException e)
        {
            logger.LogWarning(e, "Failed to parse danmaku event: {Json}", json);
        }
        catch (InvalidDataException e)
        {
            logger.LogWarning(e, "Failed to parse danmaku event data: {Json}", json);
        }
    }

    public async Task ReceiveInteractEventsAsync(string roomId,
        Func<EnterRoomEvent, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/api/b/live/{roomId}/event/subscribe?type=interact";
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
                await DispatchInteractAsync(data, handler, cancellationToken);
                data.Clear();
                continue;
            }
            if (line.StartsWith("data:")) data.AppendLine(line["data:".Length..].TrimStart());
        }
        if (data.Length > 0) await DispatchInteractAsync(data, handler, cancellationToken);
    }

    private async ValueTask DispatchInteractAsync(StringBuilder data, Func<EnterRoomEvent, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        var json = data.ToString().Trim();
        if (json.Length == 0) return;
        try
        {
            var item = JsonSerializer.Deserialize<InteractEventItem>(json);
            if (item?.Payload is { Cmd: "INTERACT_WORD_V2", Data: { } rawData })
            {
                var enterEvent = rawData.Parse();
                await handler(enterEvent, cancellationToken);
            }
        }
        catch (JsonException e)
        {
            logger.LogWarning(e, "Failed to parse interact event: {Json}", json);
        }
        catch (InvalidDataException e)
        {
            logger.LogWarning(e, "Failed to parse interact event data: {Json}", json);
        }
    }
}

public class VtuberUserInfo
{
    [JsonPropertyName("uId")] public long UId { get; set; }

    [JsonPropertyName("uName")] public string UName { get; set; } = "";

    [JsonPropertyName("roomId")] public long RoomId { get; set; }
}

public class DanmakuEventItem
{
    [JsonPropertyName("roomId")] public long RoomId { get; set; }

    [JsonPropertyName("payload")] public DanmakuEventPayload? Payload { get; set; }
}

public class DanmakuEventPayload
{
    [JsonPropertyName("cmd")] public string Cmd { get; set; } = "";

    [JsonPropertyName("info")] public DanmuMsg Info { get; set; }
}

public class InteractEventItem
{
    [JsonPropertyName("roomId")] public long RoomId { get; set; }

    [JsonPropertyName("payload")] public InteractEventPayload? Payload { get; set; }
}

public class InteractEventPayload
{
    [JsonPropertyName("cmd")] public string Cmd { get; set; } = "";

    [JsonPropertyName("data")] public InteractWordV2 Data { get; set; }
}
