using System.Text.Json;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.AI.Agents;

public class AgentSessionManager(IJsonConfig<AgentSessionConfig> config)
{
    public ValueTask ClearSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return config.BeginConfigMutationScopeAsync(async (cfg, ct) =>
        {
            cfg.Storage.Remove(sessionId);
            await config.SaveAsync(cfg, ct);
        }, cancellationToken);
    }

    public ValueTask<JsonElement?> GetSessionAsync(string sessionId,
        CancellationToken cancellationToken = default)
    {
        return config.BeginConfigMutationScopeAsync<JsonElement?>(async (cfg, ct) =>
        {
            if (!config.Current.Storage.TryGetValue(sessionId, out var raw))
            {
                return raw;
            }
            return null;
        }, cancellationToken);
    }
    
    public ValueTask SaveSessionAsync(string sessionId, JsonElement session,
        CancellationToken cancellationToken = default)
    {
        return config.BeginConfigMutationScopeAsync(async (cfg, ct) =>
        {
            cfg.Storage[sessionId] = session;
            await config.SaveAsync(cfg, ct);
        }, cancellationToken);
    }
}