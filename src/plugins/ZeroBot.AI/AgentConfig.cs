using System.Text.Json;

namespace ZeroBot.AI;

public record AgentConfig(Dictionary<long, JsonElement> Storage)
{
    public static AgentConfig Default => new([]);
}
