namespace ZeroBot.TestPlugin.Config;

public record EmotionConfig(string BaseDir, Dictionary<string, string> Commands)
{
    public static EmotionConfig Default => new("emotions", new Dictionary<string, string>());
}
