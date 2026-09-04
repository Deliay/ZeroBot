using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ZeroBot.Bilibili.Dynamic;

public record VtuberServerOptions(string Endpoint);

public class VtuberSpaceApi(HttpClient http, VtuberServerOptions options, ILogger<VtuberSpaceApi> logger)
{
    private string BaseUrl => options.Endpoint.TrimEnd('/');

    public async Task SubscribeAsync(string mid, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync($"{BaseUrl}/api/b/subscription", new { mid, type = "dynamic" }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UnsubscribeAsync(string mid, CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"{BaseUrl}/api/b/subscription/{mid}?type=dynamic", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<VtuberSpaceItem?> GetLatestDynamicAsync(string mid, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.GetFromJsonAsync<VtuberSpaceResponse>(
                $"{BaseUrl}/api/b/user/{mid}/space?page=1&pageSize=1", cancellationToken);
            return response?.Items.FirstOrDefault();
        }
        catch (Exception e)
        {
            logger.LogError(e, "GetLatestDynamicAsync Exception, mid: {Mid}", mid);
            return null;
        }
    }
}

public class VtuberSpaceResponse
{
    [JsonPropertyName("items")] public List<VtuberSpaceItem> Items { get; set; } = [];
}

public class VtuberSpaceItem
{
    [JsonPropertyName("dynamicId")] public string DynamicId { get; set; } = "";

    [JsonPropertyName("data")] public DynamicData? Data { get; set; }
}

public class DynamicData
{
    [JsonPropertyName("id_str")] public string IdStr { get; set; } = "";

    [JsonPropertyName("type")] public string Type { get; set; } = "";

    [JsonPropertyName("modules")] public DynamicModules? Modules { get; set; }

    [JsonPropertyName("orig")] public DynamicData? Orig { get; set; }
}

public class DynamicModules
{
    [JsonPropertyName("module_author")] public ModuleAuthor? ModuleAuthor { get; set; }

    [JsonPropertyName("module_dynamic")] public ModuleDynamic? ModuleDynamic { get; set; }
}

public class ModuleAuthor
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public class ModuleDynamic
{
    [JsonPropertyName("desc")] public DynamicRichText? Desc { get; set; }

    [JsonPropertyName("major")] public DynamicMajor? Major { get; set; }
}

public class DynamicMajor
{
    [JsonPropertyName("opus")] public DynamicOpus? Opus { get; set; }

    [JsonPropertyName("live_rcmd")] public DynamicLiveRcmd? LiveRcmd { get; set; }
}

public class DynamicLiveRcmd
{
    [JsonPropertyName("content")] public string? Content { get; set; }
}

public class LiveRcmdContent
{
    [JsonPropertyName("live_play_info")] public LivePlayInfo? LivePlayInfo { get; set; }
}

public class LivePlayInfo
{
    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("cover")] public string? Cover { get; set; }

    [JsonPropertyName("link")] public string? Link { get; set; }

    [JsonPropertyName("room_id")] public long RoomId { get; set; }

    [JsonPropertyName("online")] public int Online { get; set; }

    [JsonPropertyName("area_name")] public string? AreaName { get; set; }
}

public class DynamicOpus
{
    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("summary")] public DynamicRichText? Summary { get; set; }

    [JsonPropertyName("pics")] public List<DynamicPic> Pics { get; set; } = [];

    [JsonPropertyName("jump_url")] public string? JumpUrl { get; set; }
}

public class DynamicRichText
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";

    [JsonPropertyName("rich_text_nodes")] public List<DynamicRichTextNode> RichTextNodes { get; set; } = [];
}

public class DynamicRichTextNode
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";

    [JsonPropertyName("type")] public string Type { get; set; } = "";
}

public class DynamicPic
{
    [JsonPropertyName("url")] public string Url { get; set; } = "";
}
