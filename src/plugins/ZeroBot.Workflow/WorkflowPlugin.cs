using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Workflow;

public class WorkflowPlugin(IConfiguration config) : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();

        // 配置节 WorkflowService，例如环境变量 WorkflowService__BaseUrl。
        services.Configure<WorkflowOptions>(config.GetSection("WorkflowService"));
        // 群开关（/workflow:enable、/workflow:disable 维护），热加载 workflow-config.json。
        services.ConfigureJsonConfig("workflow-config.json", WorkflowGroupOptions.Default, cancellationToken);
        services.AddSingleton<WorkflowApi>();
        services.AddSingletonComponent<WorkflowManageCommandHandler>();
        services.AddSingletonComponent<WorkflowCommandHandler>();

        return ValueTask.FromResult(services);
    }
}
