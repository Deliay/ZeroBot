using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace ZeroBot.Synthesize;

/// <summary>
/// 语音训练服务合成接口客户端。
/// </summary>
public class SynthesizeApi(HttpClient http, ILogger<SynthesizeApi> logger)
{
    /// <summary>
    /// 调用合成接口，返回音频二进制；失败返回 null。
    /// </summary>
    public async Task<byte[]?> SynthesizeAsync(string endpoint, string datasetId, string text, string lang,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url =
                $"{endpoint.TrimEnd('/')}/api/training/datasets/{Uri.EscapeDataString(datasetId)}/synthesize";
            var response = await http.PostAsJsonAsync(url, new { text, lang }, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Synthesize request failed with status {Status}, dataset: {DatasetId}",
                    response.StatusCode, datasetId);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return bytes.Length == 0 ? null : bytes;
        }
        catch (Exception e)
        {
            logger.LogError(e, "SynthesizeAsync exception, dataset: {DatasetId}", datasetId);
            return null;
        }
    }
}
