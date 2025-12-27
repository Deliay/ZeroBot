using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Utility;

public static class BotExtensions
{
    extension(IBotContext bot)
    {
        public async ValueTask<bool> IsGroupAdminAsync(long accountId, long groupId, long userId,
            CancellationToken cancellationToken = default)
        {
            var groupMembers = await bot.GetGroupMembersAsync(accountId, groupId, cancellationToken);
            return groupMembers is not null &&
                   groupMembers.Members.Any(m => m.UserId == userId && m.Role != Role.Member);
        }
        
        public async ValueTask<IEnumerable<OutgoingSegment>> TryAtAllMembers(long accountId, long groupId,
            CancellationToken cancellationToken)
        {
            if (!await bot.IsGroupAdminAsync(accountId, groupId, accountId, cancellationToken)) return [];

            return [new MentionAllOutgoingSegment(new MentionAllOutgoingSegmentData())];
        }
    }
}
