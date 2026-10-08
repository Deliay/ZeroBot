namespace ZeroBot.Workflow;

/// <summary>
/// 工作流群开关配置（热加载 workflow-config.json）。
/// 磁盘字段名与属性名一致（PascalCase），手改配置时须使用 PascalCase。
/// </summary>
public record WorkflowGroupOptions
{
    public static WorkflowGroupOptions Default => new();

    /// <summary>
    /// 已启用工作流功能的群 PeerId 集合；不在集合内的群不允许使用工作流。
    /// 由 /workflow:enable、/workflow:disable 维护。
    /// </summary>
    public HashSet<long> EnabledGroups { get; init; } = [];
}
