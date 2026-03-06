using System.Diagnostics.CodeAnalysis;
using ZeroBot.Endfield.Card.BrowserRender.Abstraction;

namespace ZeroBot.Endfield.Card.BrowserRender.Service;

internal class RenderContextProvider : IRenderContextProvider
{
    private readonly Dictionary<string, IDictionary<string, object?>> _renderContext = new();
    
    public void Add(string uniqueId, IDictionary<string, object?> parameters)
    {
        _renderContext.Add(uniqueId, parameters);
    }

    public void Remove(string uniqueId)
    {
        _renderContext.Remove(uniqueId);
    }

    public bool Contains(string uniqueId)
    {
        return _renderContext.ContainsKey(uniqueId);
    }

    public bool TryGetValue(string? uniqueId, [NotNullWhen(true)] out IDictionary<string, object?>? parameters)
    {
        if (uniqueId is not null) return _renderContext.TryGetValue(uniqueId, out parameters);
        
        parameters = null;
        return false;
    }
}
