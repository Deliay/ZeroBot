using System.Diagnostics.CodeAnalysis;
using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.AI.Chats;

public class ChatProviderManager(IJsonConfig<LlmProviderConfig> config, IServiceProvider sp) : IComponentInitializer
{
    private Dictionary<string, IChatProvider> _providers = new();


    private void UpdateProviders(LlmProviderConfig llmConfig)
    {
        foreach (var currentProvider in llmConfig.Providers)
        {   
            var provider = currentProvider.Type switch
            {
                ProviderType.OpenAI => sp.GetRequiredService<GenericOpenAIProvider>(),
                _ => throw new NotImplementedException(),
            };
            
            provider.UpdateConfig(currentProvider);
            _providers[currentProvider.Id] = provider;
        }
    }

    private async ValueTask WatchConfig(CancellationToken cancellationToken = default)
    {
        await foreach (var newConfig in config.WatchChangesAsync(cancellationToken))
        {
            UpdateProviders(newConfig);;
        }
    }
    
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        await config.WaitForInitializedAsync(cancellationToken);
        UpdateProviders(config.Current);
    }

    public bool TryGetModel(string providerId, string modelId, [NotNullWhen(true)] out IChatClient? model)
    {
        if (_providers.TryGetValue(providerId, out var provider))
        {
            model = provider.GetModel(modelId);
            return true;
        }
        
        model = null;
        return false;
    }
    
    public IEnumerable<string> Providers => _providers.Values.Select(p => p.Id);

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}