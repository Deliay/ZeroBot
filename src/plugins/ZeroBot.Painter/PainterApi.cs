using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace ZeroBot.Painter;

/// <summary>
/// 绘图接口调用结果：成功时 <see cref="Data"/> 为图片二进制；
/// 失败时 <see cref="Error"/> 为可直接回复到聊天中的错误信息（尽力包含上游返回的错误详情），
/// 无上游详情时为 null，由调用方回落到通用失败提示。
/// </summary>
public sealed record PaintResult(bool Success, byte[]? Data, string? Error)
{
    public static PaintResult Ok(byte[] data) => new(true, data, null);

    public static PaintResult Fail(string? error) => new(false, null, error);
}

/// <summary>
/// 绘图训练服务接口客户端。
/// 自持专用 <see cref="HttpClient"/>（实例级超时置为无限），唯一超时来源是请求级
/// <see cref="CancellationTokenSource"/>，因此热加载修改 <c>HttpTimeoutSeconds</c> 无需重启即生效。
/// </summary>
public class PainterApi(ILogger<PainterApi> logger)
{
    // 专用实例：Timeout 置为 Infinite，唯一超时来源是请求级 CTS（可热加载）；
    // 也不与 SynthesizePlugin 注册的 AddSingleton<HttpClient>() 抢解析。
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>回复到聊天中的错误信息最大长度，超出截断，避免上游长响应刷屏。</summary>
    private const int MaxErrorLength = 300;

    /// <summary>
    /// 以 multipart/form-data 调用绘图接口，成功返回图片二进制；
    /// 失败（非 2xx / 超时 / 异常 / 空体 / JSON 响应）返回 <see cref="PaintResult"/>，尽量带上上游错误详情。
    /// </summary>
    public async Task<PaintResult> GenerateAsync(string endpoint, string prompt, IReadOnlyList<byte[]> images,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(prompt), "text");
            for (var i = 0; i < images.Count; i++)
            {
                // 按真实字节嗅探类型：QQ 图片可能是 PNG/WebP/GIF 等，不能一律声称为 jpeg。
                var (contentType, extension) = DetectImageType(images[i]);
                var content = new ByteArrayContent(images[i]);
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                form.Add(content, "image", $"image-{i}.{extension}");
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var url = $"{endpoint.TrimEnd('/')}/api/training/images/generate";
            using var response = await Http.PostAsync(url, form, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                logger.LogError("绘图请求失败，状态码 {Status}，响应 {Body}",
                    response.StatusCode, Truncate(body, MaxErrorLength));
                return PaintResult.Fail(DescribeFailure(response, body));
            }

            // 防御分支：接口若改为 JSON 结构响应，本期不解析，避免把 JSON 当图片发出；
            // 但尽量把响应内容（通常是错误详情）回传给用户，便于定位问题。
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                logger.LogError("绘图接口返回 JSON（{MediaType}），本期不支持解析，按失败处理：{Body}",
                    mediaType, Truncate(body, MaxErrorLength));

                var detail = NormalizeBody(body);
                return PaintResult.Fail(string.IsNullOrWhiteSpace(detail)
                    ? "绘图接口返回了非图片响应"
                    : $"绘图接口返回了非图片响应：{detail}");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cts.Token);
            return bytes.Length == 0
                ? PaintResult.Fail("绘图接口返回了空响应")
                : PaintResult.Ok(bytes);
        }
        catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            // 外层令牌未取消 → 请求级 CTS 触发，即超时。
            var seconds = timeout.TotalSeconds;
            logger.LogError(e, "绘图请求超时（{TimeoutSeconds} 秒）", seconds);
            return PaintResult.Fail($"绘图请求超时（{seconds:0.##} 秒）");
        }
        catch (Exception e)
        {
            logger.LogError(e, "绘图请求异常");
            return PaintResult.Fail(null);
        }
    }

    /// <summary>
    /// 拼装非 2xx 的失败信息，形如 <c>上游返回 500 Internal Server Error：{响应体}</c>。
    /// </summary>
    private static string DescribeFailure(HttpResponseMessage response, string body)
    {
        var status = $"上游返回 {(int)response.StatusCode} {response.StatusCode}";
        var detail = NormalizeBody(body);
        return string.IsNullOrWhiteSpace(detail) ? status : $"{status}：{detail}";
    }

    /// <summary>
    /// 把上游响应体按原文整理为可直接回复的错误信息：去首尾空白并截断；空白响应返回 null。
    /// </summary>
    public static string? NormalizeBody(string? body)
        => string.IsNullOrWhiteSpace(body) ? null : Truncate(body.Trim(), MaxErrorLength);

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "…";

    /// <summary>
    /// 按文件魔数嗅探图片类型，返回 (Content-Type, 扩展名)；未知类型回落到通用二进制。
    /// </summary>
    public static (string ContentType, string Extension) DetectImageType(byte[] data)
    {
        if (data.Length >= 8
            && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            return ("image/png", "png");
        }

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return ("image/jpeg", "jpg");
        }

        if (data.Length >= 4
            && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
        {
            return ("image/gif", "gif");
        }

        if (data.Length >= 12
            && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
            && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
        {
            return ("image/webp", "webp");
        }

        if (data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4D)
        {
            return ("image/bmp", "bmp");
        }

        return ("application/octet-stream", "bin");
    }
}
