namespace ZeroBot.TestPlugin.Config;

public record PuzzleSolverConfig(string Endpoint)
{
    public static PuzzleSolverConfig Default = new("http://localhost:9000");
}
