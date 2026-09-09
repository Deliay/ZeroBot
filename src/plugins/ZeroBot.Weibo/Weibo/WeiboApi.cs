using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ZeroBot.Weibo.Weibo;

public record VtuberServerOptions(string Endpoint);

public class WeiboApi(HttpClient http, VtuberServerOptions options, ILogger<WeiboApi> logger)
{
    private string BaseUrl => options.Endpoint.TrimEnd('/');

    public async Task<bool> SubscribeAsync(string uid, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync($"{BaseUrl}/api/weibo/subscription", new { uid }, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e)
        {
            logger.LogError(e, "SubscribeAsync Exception, uid: {Uid}", uid);
            return false;
        }
    }

    public async Task<WeiboTimelineItem?> GetLatestWeiboAsync(string uid, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.GetFromJsonAsync<WeiboTimelineResponse>(
                $"{BaseUrl}/api/weibo/user/{uid}/timeline?page=1&pageSize=1", cancellationToken);
            return response?.Items.FirstOrDefault();
        }
        catch (Exception e)
        {
            logger.LogError(e, "GetLatestWeiboAsync Exception, uid: {Uid}", uid);
            return null;
        }
    }
}

public class WeiboTimelineResponse
{
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("page")] public int Page { get; set; }
    [JsonPropertyName("pageSize")] public int PageSize { get; set; }
    [JsonPropertyName("items")] public List<WeiboTimelineItem> Items { get; set; } = [];
}

public class WeiboTimelineItem
{
    [JsonPropertyName("uid")] public long Uid { get; set; }
    [JsonPropertyName("mblogId")] public string MblogId { get; set; } = "";
    [JsonPropertyName("fetchedAt")] public string? FetchedAt { get; set; }
    [JsonPropertyName("data")] public WeiboStatus? Data { get; set; }
}

public class WeiboStatus
{
    [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("isLongText")] public bool IsLongText { get; set; }
    [JsonPropertyName("pic_ids")] public List<string>? PicIds { get; set; }
    [JsonPropertyName("pics")] public List<WeiboPic>? Pics { get; set; }
    [JsonPropertyName("original_pic")] public string? OriginalPic { get; set; }
    [JsonPropertyName("user")] public WeiboUser? User { get; set; }
    [JsonPropertyName("retweeted_status")] public WeiboStatus? RetweetedStatus { get; set; }
}

public class WeiboPic
{
    [JsonPropertyName("pid")] public string? Pid { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("large")] public WeiboPicSize? Large { get; set; }
}

public class WeiboPicSize
{
    [JsonPropertyName("url")] public string? Url { get; set; }
}

public class WeiboUser
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("screen_name")] public string? ScreenName { get; set; }
}