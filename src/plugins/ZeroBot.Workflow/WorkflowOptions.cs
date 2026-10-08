namespace ZeroBot.Workflow;

/// <summary>
/// 工作流插件配置（绑定配置节 <c>WorkflowService</c>）。
/// 例如环境变量 <c>WorkflowService__BaseUrl</c>，或 appsettings.json 中的
/// <c>{ "WorkflowService": { "BaseUrl": "..." } }</c>。
/// </summary>
public record WorkflowOptions
{
    /// <summary>
    /// 工作流服务基地址，例如 <c>http://workflow-service:8080</c>。
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// 单次请求超时时间（秒），默认 600。
    /// </summary>
    public int HttpTimeoutSeconds { get; init; } = 600;
}
