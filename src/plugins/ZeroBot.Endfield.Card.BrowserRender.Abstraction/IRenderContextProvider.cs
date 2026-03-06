using System.Diagnostics.CodeAnalysis;

namespace ZeroBot.Endfield.Card.BrowserRender.Abstraction;

public interface IRenderContextProvider
{
    public void Add(string uniqueId, IDictionary<string, object?> parameters);
    public void Remove(string uniqueId);
    public bool Contains(string uniqueId);
    public bool TryGetValue(string? uniqueId, [NotNullWhen(true)] out IDictionary<string, object?>? parameters);
}
