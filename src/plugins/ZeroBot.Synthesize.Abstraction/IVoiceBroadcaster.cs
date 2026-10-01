namespace ZeroBot.Synthesize.Abstraction;

/// <summary>
/// 通知语音播报契约。由语音合成插件（ZeroBot.Synthesize）实现，通知插件可选消费。
/// 消费方以 <c>IEnumerable&lt;IVoiceBroadcaster&gt;</c> 注入，未注册时解析为空集合即静默不播报。
/// 实现必须自行消化全部异常，不得影响调用方的通知主流程。
/// </summary>
public interface IVoiceBroadcaster
{
    /// <summary>
    /// 对开启了语音播报的群，将 <paramref name="text"/> 合成语音并作为独立消息发送；
    /// <paramref name="groupIds"/> 中未开启播报的群由实现自动过滤。
    /// </summary>
    ValueTask BroadcastAsync(IReadOnlyCollection<long> groupIds, string text,
        CancellationToken cancellationToken = default);
}
