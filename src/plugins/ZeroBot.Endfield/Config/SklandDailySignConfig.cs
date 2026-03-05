namespace ZeroBot.Endfield.Config;

public readonly record struct SignTask(long selfId, long userId, string credentialId);

public record SklandDailySignConfig(
    HashSet<SignTask> AutoSignTasks,
    Dictionary<string, DateTimeOffset> LastSignedAt)
{
    public static SklandDailySignConfig Empty => new([], []);
}
