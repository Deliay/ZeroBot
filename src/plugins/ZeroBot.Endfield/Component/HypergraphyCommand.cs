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

public class HypergraphyCommand(
    HypergryphClient client,
    CredentialManager credentialManager,
    ScanQrCodeTaskManager taskManager,
    ICommandDispatcher dispatcher,
    DailySignPeriodicTask periodicTask,
    IBotContext bot) : CommandQueuedHandler(dispatcher)
{
    private static readonly TextOutgoingSegment HelpStrings = ("鹰角小助手\n" +
                                       "===私聊指令===\n" +
                                       "/鹰角:绑定\n" +
                                       "/鹰角:已绑\n" +
                                       "/鹰角:解绑:本地ID\n" +
                                       "/鹰角:自动签到:本地ID (添加时会自动进行签到)\n" +
                                       "/鹰角:关闭自动签到:本地ID\n" +
                                       "\n===全局指令===\n" +
                                       "以后会有我的信息，我的体力一类的，我的抽卡记录等信息，群聊可用，但现在暂时还没有").ToMilkyTextSegment();

    private static readonly TextOutgoingSegment QrCodeGeneratedStrings = "已生成登录二维码，请扫描或等待5分钟后再试".ToMilkyTextSegment();
    private static readonly TextOutgoingSegment BindingRemovedStrings = "已移除绑定".ToMilkyTextSegment();
    protected override ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(message.ToText().Trim().StartsWith("/鹰角"));
    }

    private async ValueTask BindingAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
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
                $"帐号(本地ID: {credential.Id})中的下列角色绑定成功:\n{string.Join("\n", bindings)}".ToMilkyTextSegment()
            ]);
        }, cancellationToken);
    }

    private async ValueTask MyBindingsAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
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

    private async ValueTask UnboundAsync(string id, Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        message.EnsureIsPrivateMessage();
        var senderId = $"{message.Data.SenderId}";
        await credentialManager.RemoveCredentialAsync(senderId, id.Trim(), cancellationToken);

        await message.ReplyAsPrivate(bot, cancellationToken, [BindingRemovedStrings]);

        ArrayPool<object>.Shared.Rent(10);
    }

    private async ValueTask EnabledDailySignAsync(string id, Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        message.EnsureIsPrivateMessage();
        var task = new SignTask(message.SelfId, message.Data.SenderId, id);
        await periodicTask.AddTaskAsync(task, cancellationToken);
    }

    private async ValueTask DisableDailySignAsync(string id, Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        message.EnsureIsPrivateMessage();
        var task = new SignTask(message.SelfId, message.Data.SenderId, id);
        await periodicTask.RemoveTaskAsync(task, cancellationToken);
    }

    private ValueTask HelpAsync(Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        return message.Reply(bot, cancellationToken, [HelpStrings]);
    }
    
    protected ValueTask DequeueCoreAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        var cmd = @event.ToTextCommands(argumentSplitters: ":：").First();
        if (cmd.Arguments.Length == 0) return HelpAsync(@event, cancellationToken);

        return cmd.Arguments[0] switch
        {
            "绑定" => BindingAsync(@event, cancellationToken),
            "已绑" => MyBindingsAsync(@event, cancellationToken),
            "解绑" when (cmd.Arguments.Length == 2) => UnboundAsync(cmd.Arguments[1], @event,
                cancellationToken),
            "自动签到" when (cmd.Arguments.Length == 2) => EnabledDailySignAsync(cmd.Arguments[1], @event, cancellationToken),
            "关闭自动签到" when (cmd.Arguments.Length == 2) => DisableDailySignAsync(cmd.Arguments[1], @event, cancellationToken),
            _ => HelpAsync(@event, cancellationToken)
        };
    }

    protected override async ValueTask DequeueAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await DequeueCoreAsync(@event, cancellationToken);
        }
        catch (Exception e)
        {
            await @event.Reply(bot, cancellationToken, [e.Message.ToMilkyTextSegment()]);
        }
    }
}