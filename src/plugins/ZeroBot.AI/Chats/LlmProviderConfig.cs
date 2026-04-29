namespace ZeroBot.AI.Chats;

public enum ProviderType
{
    OpenAI,
    Authropic,
}

public readonly record struct ProviderConfig(
    ProviderType Type,
    string Id,
    string Endpoint,
    string ApiKey);

public record LlmProviderConfig(List<ProviderConfig> Providers)
{
    public static LlmProviderConfig Default => new LlmProviderConfig([]);
}
