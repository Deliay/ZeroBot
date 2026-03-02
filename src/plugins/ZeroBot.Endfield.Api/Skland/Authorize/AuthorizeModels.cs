namespace ZeroBot.Endfield.Api.Skland.Authorize;

public class DesRuleEntry
{
    public string? Cipher { get; set; }
    public required int IsEncrypt { get; set; }
    public string? Key { get; set; }
    public required string ObfuscatedName { get; set; }
}



public class Credential
{
    public required string Token { get; set; }
    public required string Cred { get; set; }
    public required string DeviceId { get; set; }
}