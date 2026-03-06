using System.Buffers;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Endfield.Api.Skland;
using ZeroBot.Endfield.Api.Skland.Authorize;
using ZeroBot.Endfield.Api.Skland.Player;
using ZeroBot.Endfield.Config;
using ZeroBot.Endfield.Extension;
using ZeroBot.Utility;

namespace ZeroBot.Endfield.Component;

public class BindingCommandHandlers(
    HypergryphClient client,
    CredentialManager credentialManager,
    ScanQrCodeTaskManager taskManager,
    DailySignPeriodicTask periodicTask,
    IBotContext bot)
{
    private static readonly TextOutgoingSegment QrCodeGeneratedStrings = "已生成登录二维码，请扫描或等待5分钟后再试".ToMilkyTextSegment();
    private static readonly TextOutgoingSegment BindingRemovedStrings = "已移除绑定".ToMilkyTextSegment();

    public async ValueTask BindingAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        message.EnsureIsPrivateMessage();
        var senderId = $"{message.Data.SenderId}";

        var scanId = await credentialManager.GetUserScanIdAsync(senderId, cancellationToken);
        if (scanId is not null)
        {
            await message.SendAsPrivate(bot, cancellationToken, [QrCodeGeneratedStrings]);
            return;
        }
        
        var qrCode = await credentialManager.GenerateLoginQrCodePayload(senderId, cancellationToken);
        
        await message.SendAsPrivate(bot, cancellationToken, [
            qrCode.ToPngQrCodeByteArray().ToMilkyImageSegment(),
            QrCodeGeneratedStrings]);
        
        taskManager.Enqueue(senderId, async (credential) =>
        {
            var bindings = (await client.GetPlayerBindings(credential, cancellationToken))
                .Flat()
                .Select((binding) => $"- {binding}");
            await message.SendAsPrivate(bot, cancellationToken, [
                ($"帐号(本地ID: {credential.Id})中的下列角色绑定成功:\n" +
                 $"{string.Join("\n", bindings)}\n\n" +
                 $"自动签到已自动开启，如果无需自动签到，请手动关闭").ToMilkyTextSegment()
            ]);
            var task = new SignTask(message.SelfId, message.Data.SenderId, credential.Id);
            await periodicTask.AddTaskAsync(task, cancellationToken);
        }, cancellationToken);
    }

    public async ValueTask MyBindingsAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        message.EnsureIsPrivateMessage();
        var senderId = $"{message.Data.SenderId}";
        await credentialManager.RenewalRefreshTokenAsync(senderId, cancellationToken);
        var credentials = await credentialManager.GetCurrentCredentialAsync(senderId, cancellationToken);

        var allBindings = await credentials.ToAsyncEnumerable()
            .Select(async (credential, token) =>
            {
                var roles = string.Join('\n', (await client.GetPlayerBindings(credential, token))
                    .Flat().Select(binding => $"- {binding}"));
                return $"帐号(本地ID: {credential.Id})，下的角色:\n{roles}";
            })
            .AggregateAsync((a, b) => a + b, cancellationToken);
        
        await message.ReplyAsPrivate(bot, cancellationToken, [allBindings.ToMilkyTextSegment()]);
    }

    public async ValueTask UnboundAsync(string id, Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        message.EnsureIsPrivateMessage();
        var senderId = $"{message.Data.SenderId}";
        await credentialManager.RemoveCredentialAsync(senderId, id.Trim(), cancellationToken);

        await message.ReplyAsPrivate(bot, cancellationToken, [BindingRemovedStrings]);

        ArrayPool<object>.Shared.Rent(10);
    }

    public async ValueTask EnabledDailySignAsync(string id, Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        message.EnsureIsPrivateMessage();
        var task = new SignTask(message.SelfId, message.Data.SenderId, id);
        var userId = $"{message.Data.SenderId}";
        var credential = await credentialManager.GetCredentialAsync(userId, id, cancellationToken);
        if (credential is null)
        {
            await message.Reply(bot, cancellationToken, [
                $"本地ID：「{id}」不存在，请输入类似于 00000000000-0000-000-0000000 格式的本地ID".ToMilkyTextSegment()
            ]);
            return;
        }
        await periodicTask.AddTaskAsync(task, cancellationToken);
    }

    public async ValueTask DisableDailySignAsync(string id, Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        message.EnsureIsPrivateMessage();
        var task = new SignTask(message.SelfId, message.Data.SenderId, id);
        await periodicTask.RemoveTaskAsync(task, cancellationToken);
    }
}