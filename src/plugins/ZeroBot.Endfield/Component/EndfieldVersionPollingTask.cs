using EmberFramework.Abstraction;
using Microsoft.Extensions.Logging;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Endfield.Config;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Endfield.Component;

public class EndfieldVersionPollingTask(
    IJsonConfig<EndfieldVersionSubscriptionConfig> config,
    HttpClient httpClient,
    ILogger<EndfieldVersionPollingTask> logger,
    IBotContext bot) : IExecutable
{
    private const string VersionEndpoint = "https://endfield-assets.fffdan.com/version";
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        await config.WaitForInitializedAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Version polling failed");
            }
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private async ValueTask PollAsync(CancellationToken cancellationToken)
    {
        var version = await httpClient.GetStringAsync(VersionEndpoint, cancellationToken);
        if (string.IsNullOrWhiteSpace(version)) return;

        version = version.Trim();

        if (config.Current.LastKnownVersion is null)
        {
            await SaveVersionAsync(version, cancellationToken);
            return;
        }

        if (version == config.Current.LastKnownVersion) return;

        await NotifySubscribersAsync(version, cancellationToken);
        await SaveVersionAsync(version, cancellationToken);
    }

    private async ValueTask NotifySubscribersAsync(string newVersion, CancellationToken cancellationToken)
    {
        if (config.Current.SubscribedGroupIds.Count == 0) return;

        var message = $"终末地版本更新！新版本: {newVersion}".ToMilkyTextSegment();
        await foreach (var (accountId, _) in bot.GetAccountInfoAsync(cancellationToken))
        {
            await bot.WriteManyGroupMessageAsync(
                accountId,
                config.Current.SubscribedGroupIds,
                cancellationToken,
                message);
        }
    }

    private ValueTask SaveVersionAsync(string version, CancellationToken cancellationToken)
    {
        return config.BeginConfigMutationScopeAsync((value, token) =>
        {
            var updated = value with { LastKnownVersion = version };
            return config.SaveAsync(updated, token);
        }, cancellationToken);
    }
}
