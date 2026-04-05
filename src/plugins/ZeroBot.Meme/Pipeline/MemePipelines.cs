using MemeFactory.Core.Processing;
using ZeroBot.Utility.Commands;

namespace ZeroBot.Meme.Pipeline;

public readonly record struct MemePipelineError(string Command, string Message);

public readonly record struct MemePipelineResult(IAsyncEnumerable<Frame> Frames, List<MemePipelineError> Errors)
{
    public MemePipelineResult CombineError(MemePipelineResult other)
    {
        return this with
        {
            Errors = [..Errors.Concat(other.Errors)],
        };
    }
}

public delegate MemePipelineResult MemeComposer(IAsyncEnumerable<Frame> frames,
    CancellationToken cancellationToken = default);

public delegate IAsyncEnumerable<Frame> MemeFactory(ITextCommand command, IAsyncEnumerable<Frame> frames,
    CancellationToken cancellationToken = default);

[AttributeUsage(AttributeTargets.Method)]
public sealed class RegisterMemeFactoryAttribute(string help, params string[] commands) : Attribute
{
    public string Help => help;
    public string[] Commands => commands;    
}

public class MemePipeline
{
    private readonly Dictionary<string, MemeFactory> Factories = new();
    private readonly Dictionary<string, string> FactoryHelpers = new();

    public void Register(string command, string help, MemeFactory factory)
    {
        Factories.Add(command, factory);
        FactoryHelpers.Add(command, help);
    }
    
    public void Register(string command, string help, Func<MemeFactory> factoryGetter)
        => Register(command, help, factoryGetter());
    
    
}
