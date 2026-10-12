using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ZeroBot.Workflow;

/// <summary>
/// 工作流接口调用结果：成功时 <see cref="Data"/> 为运行结果（编译阶段为 null），
/// 失败时 <see cref="Error"/> 为可直接回复到聊天中的错误信息。
/// </summary>
public sealed record WorkflowResult(bool Success, byte[]? Data, string? Error);

/// <summary>
/// 工作流服务客户端：先调用 <c>/v1/compile</c> 校验脚本可展开，再调用 <c>/v1/run</c> 执行。
/// 专用 <see cref="HttpClient"/>（实例级超时置为无限），唯一超时来源是请求级 <see cref="CancellationTokenSource"/>。
/// </summary>
public class WorkflowApi(ILogger<WorkflowApi> logger)
{
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>
    /// 单个二进制输入：<paramref name="Name"/> 对应脚本中的 <c>[input:name]</c>（缺省为 <c>input</c>）。
    /// </summary>
    public sealed record BinaryInput(string Name, byte[] Data);

    private sealed record WorkflowValueSpec(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("data")] string Data);

    private sealed record WorkflowRequest(
        [property: JsonPropertyName("script")] string Script,
        [property: JsonPropertyName("input_types")] IReadOnlyDictionary<string, string> InputTypes,
        [property: JsonPropertyName("inputs")] IReadOnlyDictionary<string, WorkflowValueSpec> Inputs);

    /// <summary>
    /// 调用 <c>/v1/compile</c> 测试脚本展开；不解析返回数据。
    /// <paramref name="inputTypes"/> 用于提前声明输入类型（如引用了群文件时声明 <c>input= binary</c>）。
    /// </summary>
    public Task<WorkflowResult> CompileAsync(string baseUrl, string script, TimeSpan timeout,
        IReadOnlyDictionary<string, string>? inputTypes = null,
        CancellationToken cancellationToken = default)
        => PostAsync(baseUrl, "compile", script, timeout, inputTypes, input: null, parseData: false,
            cancellationToken);

    /// <summary>
    /// 调用 <c>/v1/run</c> 执行工作流，并解析 <c>outputs.final.data</c>（base64）。
    /// <paramref name="input"/> 为可选的二进制输入（如被引用的群文件内容）。
    /// </summary>
    public Task<WorkflowResult> RunAsync(string baseUrl, string script, TimeSpan timeout,
        IReadOnlyDictionary<string, string>? inputTypes = null, BinaryInput? input = null,
        CancellationToken cancellationToken = default)
        => PostAsync(baseUrl, "run", script, timeout, inputTypes, input, parseData: true, cancellationToken);

    private async Task<WorkflowResult> PostAsync(string baseUrl, string api, string script, TimeSpan timeout,
        IReadOnlyDictionary<string, string>? inputTypes, BinaryInput? input, bool parseData,
        CancellationToken cancellationToken)
    {
        var label = api == "compile" ? "编译" : "执行";
        try
        {
            var url = $"{baseUrl.TrimEnd('/')}/v1/{api}";
            var inputs = input is null
                ? new Dictionary<string, WorkflowValueSpec>()
                : new Dictionary<string, WorkflowValueSpec>
                {
                    [input.Name] = new("binary", Convert.ToBase64String(input.Data)),
                };
            var payload = new WorkflowRequest(script, inputTypes ?? new Dictionary<string, string>(), inputs);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            using var response = await Http.PostAsJsonAsync(url, payload, cts.Token);
            var body = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("工作流{Label}请求失败，状态码 {Status}，响应 {Body}",
                    label, response.StatusCode, body);
                return new WorkflowResult(false, null,
                    $"工作流{label}失败（{(int)response.StatusCode}）：{Truncate(body, 300)}");
            }

            if (!parseData) return new WorkflowResult(true, null, null);

            var data = ExtractData(body);
            return data is null
                ? new WorkflowResult(false, null, "工作流未返回有效结果。")
                : new WorkflowResult(true, data, null);
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(e, "工作流{Label}请求超时（{TimeoutSeconds} 秒）", label, timeout.TotalSeconds);
            return new WorkflowResult(false, null, "工作流执行超时，请稍后重试。");
        }
        catch (Exception e)
        {
            logger.LogError(e, "工作流{Label}请求异常", label);
            return new WorkflowResult(false, null, "工作流服务调用异常，请稍后重试。");
        }
    }

    /// <summary>
    /// 从 <c>{ "outputs": { "final": { "data": "&lt;base64&gt;" } } }</c> 中解析二进制数据。
    /// 兼容 data URI（<c>data:...;base64,xxxx</c>）形式；无法解析时返回 null。
    /// </summary>
    private static byte[]? ExtractData(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("outputs", out var outputs)
                || !outputs.TryGetProperty("final", out var final)
                || !final.TryGetProperty("data", out var dataElement)
                || dataElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var text = dataElement.GetString();
            if (string.IsNullOrWhiteSpace(text)) return null;

            if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = text.IndexOf(',');
                if (comma >= 0) text = text[(comma + 1)..];
            }

            return Convert.FromBase64String(text);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Truncate(string value, int maxLength)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Length <= maxLength
                ? value
                : value[..maxLength] + "…";
}
