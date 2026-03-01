namespace ZeroBot.Endfield.Config;

public record PuzzleSolverConfig(string Endpoint)
{
    public static readonly PuzzleSolverConfig Default = new("http://localhost:9000");
}
