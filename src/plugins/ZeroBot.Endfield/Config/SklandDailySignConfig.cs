namespace ZeroBot.Endfield.Config;

public record SklandDailySignConfig(HashSet<string> SignedUsers)
{
    public static SklandDailySignConfig Empty => new([]);
}
