using System.ClientModel;
using OpenAI.Chat;
using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using ZeroBot.AI.Commands;
using ZeroBot.AI.Skills;
using ZeroBot.AI.Storage;
using ZeroBot.AI.Tools;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.AI;

public class AIPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();
        
        services.AddSingleton<IToolProdiver, ChatTools>();
        services.AddTransient<AgentTools>();
        services.ConfigureJsonConfig("agents.json", AgentConfig.Default, cancellationToken);
        services.AddSingleton<AgentSessionManager>();
        services.AddTransient<ISkillProvider, TrpgSkill>();
        services.AddSingleton<SkillManager>();

        services.AddSingletonComponent<TrpgCommandHandler>();
        
        var apiEndpoint = Environment.GetEnvironmentVariable("OPENAI_API_ENDPOINT") ?? throw new Exception("OPENAI_API_ENDPOINT not set");
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception("OPENAI_API_KEY not set");
        var baseClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions()
        {
            Endpoint = new Uri(apiEndpoint),
        });
        services.AddSingleton(baseClient);
        services.AddSingleton(baseClient.GetChatClient("MiniMax-M2.7").AsIChatClient());
        services.AddSingleton(sp => new ChatClientAgentOptions()
        {
            Name = "ZeroBot",
            Description = "你是在QQ群内活跃的智能体",
            ChatOptions = new ChatOptions()
            {
                Tools = sp.GetRequiredService<AgentTools>().GetTools().ToList(), 
            }
        });
        services.AddTransient<AIAgent>(sp => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(), 
            options: sp.GetRequiredService<ChatClientAgentOptions>(),
            services: sp));

        return ValueTask.FromResult(services);
    }
}