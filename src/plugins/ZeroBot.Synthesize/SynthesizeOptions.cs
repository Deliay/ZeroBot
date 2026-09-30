namespace ZeroBot.Synthesize;

/// <summary>
/// 每日生成额度记录。日期按 UTC+8 计算，格式 yyyy-MM-dd。
/// </summary>
public record DailyQuota(string Date, int Count);

/// <summary>
/// 语音合成插件配置（热加载）。
/// </summary>
public record SynthesizeOptions
{
    public const string DefaultEndpoint = "http://z-vtuber-training.vtuber.svc.cluster.local:8080";

    /// <summary>
    /// 合成服务地址，支持热加载动态修改。
    /// </summary>
    public string Endpoint { get; init; } = DefaultEndpoint;

    /// <summary>
    /// 每个群聊 + 发送人每天的生成上限。
    /// </summary>
    public int DailyLimit { get; init; } = 3;

    /// <summary>
    /// 合成语言。
    /// </summary>
    public string Lang { get; init; } = "ZH";

    /// <summary>
    /// 数据集别名 -> dataset-id。
    /// </summary>
    public Dictionary<string, string> DatasetAliases { get; init; } = [];

    /// <summary>
    /// "{peerId}:{senderId}" -> 当日额度。
    /// </summary>
    public Dictionary<string, DailyQuota> DailyQuotas { get; init; } = [];

    public static SynthesizeOptions Default => new()
    {
        Endpoint = Environment.GetEnvironmentVariable("Z_VTUBER_TRAINING_ENDPOINT") ?? DefaultEndpoint
    };
}
