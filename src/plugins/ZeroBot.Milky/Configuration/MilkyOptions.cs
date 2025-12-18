namespace ZeroBot.Milky.Configuration;

public record MilkyOptions
{
    public Uri? MilkyServer { get; init; }
    public string? AccessToken { get; init; }
}
