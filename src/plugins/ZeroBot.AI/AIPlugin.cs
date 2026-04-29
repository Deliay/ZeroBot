using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using OpenAI.Chat;
using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using ZeroBot.AI.Agents;
using ZeroBot.AI.Chats;
using ZeroBot.AI.Commands;
using ZeroBot.AI.Groups;
using ZeroBot.AI.Skills;
using ZeroBot.AI.Tools;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.AI;

public class AIPlugin : IPlugin.IWithInitializer
{
    [Experimental("MAAI001")]
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();
        
        // API providers
        services.ConfigureJsonConfig("ai-model-provider.json", LlmProviderConfig.Default, cancellationToken);
        services.AddTransient<GenericOpenAIProvider>();
        services.AddSingleton<ChatProviderManager>();
        
        // tools
        services.AddSingleton<IToolProvider, ChatTools>();
        services.AddSingleton<AgentTools>();
        
        // skills
        services.AddTransient<ISkillProvider, TrpgSkill>();
        services.AddSingleton<SkillManager>();
        
        // agent session
        services.AddTransient<GroupChatAgent>();
        services.ConfigureJsonConfig("agents.json", AgentSessionConfig.Default, cancellationToken);
        
        // agent
        services.AddSingleton<IAgentFactory, TrpgAgentFactory>();
        services.AddSingleton<AgentManager>();
        services.AddSingleton<AgentSessionManager>();

        // group agent
        services.AddSingleton<GroupAgentManager>();

        services.AddSingletonComponent<TrpgCommandHandler>();
        return ValueTask.FromResult(services);
    }

    public ValueTask InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }
}