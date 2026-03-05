using EmberFramework.Abstraction;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.TestPlugin.Components;

public class AutoAcceptFriend(IBotContext bot, ILogger<AutoAcceptFriend> logger) : IExecutable
{
    private async ValueTask RunCoreAsync(CancellationToken cancellationToken = default)
    {
        var enumerator = bot.ReadEvents(cancellationToken)
            .OfType<Event<FriendRequest>>()
            .WithCancellation(cancellationToken);
        await foreach (var @event in enumerator)
        {
            logger.LogInformation("收到来自 {initiator} 的好友申请，已自动接受", @event.Data.InitiatorId);
            await bot.AcceptFriendRequestAsync(@event.SelfId, @event.Data.InitiatorUid, cancellationToken);
        }
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            await RunCoreAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
        }
    }
}
