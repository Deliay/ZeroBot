namespace ZeroBot.Endfield.Api.Skland.Sign;

public class DesRuleEntry
{
    public string? Cipher { get; set; }
    public required int IsEncrypt { get; set; }
    public string? Key { get; set; }
    public required string ObfuscatedName { get; set; }
}