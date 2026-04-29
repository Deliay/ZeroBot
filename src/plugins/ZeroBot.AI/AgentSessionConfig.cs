using System.Text.Json;

namespace ZeroBot.AI;

public record AgentSessionConfig(Dictionary<string, JsonElement> Storage)
{
    public static AgentSessionConfig Default => new([]);
}
