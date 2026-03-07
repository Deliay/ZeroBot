using EmberFramework.Abstraction;
using Microsoft.Extensions.Logging;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Endfield.Api.Skland;
using ZeroBot.Endfield.Api.Skland.Authorize;
using ZeroBot.Endfield.Api.Skland.Player;
using ZeroBot.Endfield.Config;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.Endfield.Component;

readonly record struct SingResult(bool success, bool alreadySigned, string message);

public class DailySignPeriodicTask(
    ILogger<DailySignPeriodicTask> logger,
    IJsonConfig<SklandDailySignConfig> config,
    CredentialManager credentialManager,
    HypergryphClient client,
    IBotContext bot) : IExecutable
{
    private async ValueTask<SingResult> SignCoreAsync(SignTask task, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始进行 Bot {botId} 的用户 {userId} 的 {account} 帐号签到", task.selfId, task.userId,
            task.credentialId);

        if (config.Current.LastSignedAt.TryGetValue(task.credentialId, out var lastSignedAt))
        {
            if (lastSignedAt >= DateTimeOffset.Now.Subtract(DateTimeOffset.Now.TimeOfDay))
            {
                logger.LogInformation("帐号 {account} 今天已经签到过了", task.credentialId);
                return new SingResult(true, true, "");
            }
        }

        var userId = $"{task.userId}";
        await credentialManager.RenewalSingleRefreshTokenAsync(userId, task.credentialId, cancellationToken);
        var credential = await credentialManager.GetCredentialAsync(userId, task.credentialId, cancellationToken);
        if (credential is null)
        {
            logger.LogInformation("帐号 {account} 登录失败，自动签到已禁用", cancellationToken);
            await RemoveTaskAsync(task, cancellationToken);
            return new SingResult(false, false, $"帐号 {task.credentialId} 登录失败，已自动从自动签到列表中移除");
        }
        logger.LogInformation("帐号 {} 登录成功", task.credentialId);
        var bindings = (await client.GetPlayerBindings(credential, cancellationToken)).Flat();
        var roleSignResultList = new List<string>();
        foreach (var userAppRole in bindings)
        {
            if (!userAppRole.IsSupportSign)
            {
                logger.LogInformation("[不支持签到] {}", userAppRole);
                continue;
            }

            try
            {
                var result = await client.DailySignAsync(credential, userAppRole, cancellationToken);
                roleSignResultList.Add($"[签到成功] {userAppRole} - {result}");
            }
            catch (Exception e)
            {
                roleSignResultList.Add($"[签到失败] {userAppRole} - {e.Message}");
            }
        }

        var signResult = string.Join('\n', roleSignResultList);

        await config.BeginConfigMutationScopeAsync((newly, token) =>
        {
            newly.LastSignedAt[task.credentialId] = DateTimeOffset.Now;;

            return config.SaveAsync(newly, token);
        }, cancellationToken);

        return new SingResult(true, false, signResult);
    }

    private async ValueTask<SingResult> SignAsync(SignTask task, CancellationToken cancellationToken = default)
    {
        try
        {
            return await SignCoreAsync(task, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in DailySignPeriodicTask");
            return new SingResult(false, false, $"签到失败！失败原因：{e.Message}");
        }
    }
    
    public async ValueTask AddTaskAsync(SignTask task, CancellationToken cancellationToken = default)
    {
        await config.BeginConfigMutationScopeAsync((newly, token) =>
        {
            newly.AutoSignTasks.Add(task);
            return config.SaveAsync(newly, token);
        }, cancellationToken);

        _ = Task.Run(() => SignAsync(task, cancellationToken), cancellationToken);
    }
    
    public async ValueTask RemoveTaskAsync(SignTask task, CancellationToken cancellationToken = default)
    {
        await config.BeginConfigMutationScopeAsync((newly, token) =>
        {
            newly.AutoSignTasks.Remove(task);
            return config.SaveAsync(newly, token);
        }, cancellationToken);
    }
    
    private async ValueTask RunCoreAsync(CancellationToken cancellationToken = default)
    {
        await config.WaitForInitializedAsync(cancellationToken);
        var pendingTask = config.Current.AutoSignTasks;
        if (pendingTask.Count == 0) return;

        foreach (var account in pendingTask)
        {
            var result = await SignAsync(account, cancellationToken);

            if (result.alreadySigned) continue;
            var status = result.success ? "完成" : "出错";
            await bot.WritePrivateMessageAsync(account.selfId, account.userId, cancellationToken, [
                $"帐号 {account.credentialId} 签到{status}:\n\n{result.message}".ToMilkyTextSegment()
            ]);
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }

    public async ValueTask WaitUntilNextDayAsync(CancellationToken cancellationToken = default)
    {
        var waitTime = TimeSpan.FromHours(24) - DateTimeOffset.Now.TimeOfDay + TimeSpan.FromMinutes(1);
        logger.LogInformation("下次唤醒时间: {}", $"{DateTimeOffset.Now + waitTime:yyyy-MM-dd hh:mm:ss}");
        await Task.Delay(waitTime, cancellationToken);
    }
    
    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        if (!config.Current.SignEnabled.GetValueOrDefault(true)) return;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunCoreAsync(cancellationToken);
                await WaitUntilNextDayAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in DailySignPeriodicTask");
            }
        }
    }
}