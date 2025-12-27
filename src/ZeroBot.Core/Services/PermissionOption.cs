namespace ZeroBot.Core.Services;

public record PermissionOption
{
    public string PersistFilePath { get; set; } = "permissions.json";
}
