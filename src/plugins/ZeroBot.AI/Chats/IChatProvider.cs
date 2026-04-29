using Microsoft.Extensions.AI;

namespace ZeroBot.AI.Chats;

public interface IChatProvider
{
    public string Id { get; }
    public void UpdateConfig(ProviderConfig config);
    public IChatClient GetModel(string model);
}
