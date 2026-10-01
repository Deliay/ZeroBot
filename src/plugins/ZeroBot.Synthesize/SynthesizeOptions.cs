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
    /// 群未单独配置时使用的默认每日上限，默认为 3。
    /// -1 表示不限制；0 表示不允许；&gt;0 表示上限。
    /// </summary>
    public const int DefaultDailyLimit = 3;

    /// <summary>
    /// 合成服务地址，支持热加载动态修改。
    /// </summary>
    public string Endpoint { get; init; } = DefaultEndpoint;

    /// <summary>
    /// 每个群聊 + 发送人每天的默认生成上限（群未单独配置时使用，默认 3）。
    /// -1 表示不限制；0 表示不允许；&gt;0 表示上限。
    /// </summary>
    public int DailyLimit { get; init; } = DefaultDailyLimit;

    /// <summary>
    /// 每个群聊单独配置的每日生成上限（key 为群 PeerId），由
    /// <c>/synthesize:limit:{number}</c> 指令设置，未配置的群回落到 <see cref="DailyLimit"/>。
    /// 语义同 <see cref="DailyLimit"/>：-1 不限制；0 不允许；&gt;0 为上限。
    /// </summary>
    public Dictionary<long, int> GroupDailyLimits { get; init; } = [];

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

    /// <summary>群 PeerId → 播报音色别名；由 /动态语音播报 指令维护。</summary>
    public Dictionary<long, string> VoiceBroadcastGroups { get; init; } = [];

    /// <summary>播报文本最大长度（超出截断），默认 200；&lt;= 0 表示不截断。</summary>
    public int VoiceBroadcastMaxTextLength { get; init; } = 200;

    public static SynthesizeOptions Default => new()
    {
        Endpoint = Environment.GetEnvironmentVariable("Z_VTUBER_TRAINING_ENDPOINT") ?? DefaultEndpoint
    };
}
