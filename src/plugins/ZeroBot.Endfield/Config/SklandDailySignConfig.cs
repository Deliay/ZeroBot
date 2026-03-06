namespace ZeroBot.Endfield.Config;

public readonly record struct SignTask(long selfId, long userId, string credentialId);

public record SklandDailySignConfig(
    HashSet<SignTask> AutoSignTasks,
    Dictionary<string, DateTimeOffset> LastSignedAt,
    bool? SignEnabled = true)
{
    public static SklandDailySignConfig Empty => new([], []);
}
