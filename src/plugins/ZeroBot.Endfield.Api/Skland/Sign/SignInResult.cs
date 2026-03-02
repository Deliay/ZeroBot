namespace ZeroBot.Endfield.Api.Skland.Sign;

public class SignInResult
{
    public required bool Success { get; set; }
    public required string Game { get; set; }
    public required string Nickname { get; set; }
    public required string Channel { get; set; }
    public List<string> Awards { get; set; } = [];
    public string? Error { get; set; } = "";
}