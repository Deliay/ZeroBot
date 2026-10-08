using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Utility;

namespace ZeroBot.Workflow;

public class WorkflowPlugin(IConfiguration config) : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();

        // 配置节 WorkflowService，例如环境变量 WorkflowService__BaseUrl。
        services.Configure<WorkflowOptions>(config.GetSection("WorkflowService"));
        services.AddSingleton<WorkflowApi>();
        services.AddSingletonComponent<WorkflowCommandHandler>();

        return ValueTask.FromResult(services);
    }
}
