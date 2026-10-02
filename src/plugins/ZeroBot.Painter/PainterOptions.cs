namespace ZeroBot.Painter;

/// <summary>
/// 每日用量记录。日期按 UTC+8 计算，格式 yyyy-MM-dd。
/// </summary>
public record DailyQuota(string Date, int Count);

/// <summary>
/// 小画家绘图插件配置（热加载 painter-config.json）。
/// 磁盘字段名与属性名一致（PascalCase），手改配置时须使用 PascalCase。
/// </summary>
public record PainterOptions
{
    public const string DefaultEndpoint = "http://z-vtuber-training.vtuber.svc.cluster.local:8080";
    public const int DefaultHttpTimeoutSeconds = 600; // 10 分钟
    public const int DefaultMaxImages = 10;
    public const int DefaultMaxDailyLimit = 5;

    /// <summary>
    /// 绘图服务地址，支持热加载动态修改。
    /// </summary>
    public string Endpoint { get; init; } = DefaultEndpoint;

    /// <summary>
    /// 绘图接口专用超时（秒），默认 600；每次请求读取当前值，改配置即热生效。
    /// </summary>
    public int HttpTimeoutSeconds { get; init; } = DefaultHttpTimeoutSeconds;

    /// <summary>
    /// 单次绘图最多携带的参考图数量，超出部分忽略。
    /// </summary>
    public int MaxImages { get; init; } = DefaultMaxImages;

    /// <summary>
    /// 启用指令 {number} 的上限（PRD：1~5）。
    /// </summary>
    public int MaxDailyLimit { get; init; } = DefaultMaxDailyLimit;

    /// <summary>
    /// 群 PeerId → 每人每日上限；存在即表示该群已开启。由 /小画家:启用、/小画家:禁用 维护。
    /// </summary>
    public Dictionary<long, int> Groups { get; init; } = [];

    /// <summary>
    /// "{peerId}:{senderId}" → 当日用量，持久化跨重启；禁用群时不清除。
    /// </summary>
    public Dictionary<string, DailyQuota> DailyQuotas { get; init; } = [];

    public static PainterOptions Default => new()
    {
        Endpoint = Environment.GetEnvironmentVariable("Z_VTUBER_TRAINING_ENDPOINT") ?? DefaultEndpoint
    };
}
