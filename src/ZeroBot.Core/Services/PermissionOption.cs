namespace ZeroBot.Core.Services;

public record PermissionOption
{
    public string PersistFilePath { get; set; } = "permissions.json";
    public bool WatchFileChanges { get; set; } = true;
}
