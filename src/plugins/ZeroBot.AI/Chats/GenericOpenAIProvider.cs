using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace ZeroBot.AI.Chats;

public class GenericOpenAIProvider : IChatProvider
{
    private OpenAIClient _client = null!;

    public string Id { get; private set; } = string.Empty;

    public void UpdateConfig(ProviderConfig config)
    {
        Id = config.Id;
        _client = new OpenAIClient(new ApiKeyCredential(config.ApiKey), new OpenAIClientOptions()
        {
            Endpoint = new Uri(config.Endpoint)
        });
    }

    public IChatClient GetModel(string model)
    {
        return _client.GetChatClient(model).AsIChatClient();
    }
}
