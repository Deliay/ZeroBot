namespace ZeroBot.Endfield.Api.Skland.Sign;

public record UserBinding
{
    public required string AppCode { get; set; }
    public required string GameName { get; set; }
    public required string Nickname { get; set; }
    public required string ChannelName { get; set; }
    public required string Uid { get; set; }
    public required int GameId { get; set; }
    public List<Dictionary<string, object>> Roles { get; set; } = [];
}