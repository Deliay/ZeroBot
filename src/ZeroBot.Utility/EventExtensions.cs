using System.Runtime.CompilerServices;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility.Commands;

namespace ZeroBot.Utility;

public static class EventExtensions
{
    extension(IncomingMessage message)
    {
        public IEnumerable<ITextCommand> ToTextCommands(char prefix = '/', string argumentSplitters = ":：-")
        {

            return TextCommandParser.Parse(prefix, [..argumentSplitters], message.ToText());
        }

        public string ToText(string separator = "")
        {
            return string.Join(separator, message.Segments
                .OfType<TextIncomingSegment>()
                .Select((seg) => seg.Data.Text));
        }

        public ReplyOutgoingSegment Reply()
        {
            return new ReplyOutgoingSegment(new ReplyOutgoingSegmentData(message.MessageSeq));
        }
    }

    extension(Event<GroupIncomingMessage> message)
    {
        public async ValueTask Reply(IBotContext bot,
            CancellationToken cancellationToken = default,
            params OutgoingSegment[] segments)
        {
            await bot.WriteManyGroupMessageAsync(message.SelfId, [message.Data.PeerId], cancellationToken, segments);
        }
    }
    
    extension(Event<IncomingMessage> message)
    {
        public async ValueTask ReplyAsGroup(IBotContext bot,
            CancellationToken cancellationToken = default,
            params OutgoingSegment[] segments)
        {
            await bot.WriteManyGroupMessageAsync(message.SelfId, [message.Data.PeerId], cancellationToken,
                [message.Data.Reply(), ..segments]);
        }
        
        public MessageScene Scene => message.Data switch
        {
            GroupIncomingMessage => MessageScene.Group,
            FriendIncomingMessage => MessageScene.Friend,
            TempIncomingMessage => MessageScene.Temp,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        public async IAsyncEnumerable<ImageIncomingSegment> GetImageMessagesAsync(IBotContext bot,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {

            if (message.Data.Segments.OfType<ReplyIncomingSegment>().FirstOrDefault() is { } reply)
            {
                var replyMessage = await bot.GetHistoryMessageAsync(message.SelfId, message.Scene, message.Data.PeerId,
                    reply.Data.MessageSeq, cancellationToken);

                if (replyMessage?.Data.Segments.OfType<ImageIncomingSegment>().FirstOrDefault() is { } replyImage)
                {
                    yield return replyImage;
                }
            }
            
            if (message.Data.Segments.OfType<ImageIncomingSegment>().FirstOrDefault() is { } image)
            {
                yield return image;
            }
        }
    }

    extension(string message)
    {
        public TextOutgoingSegment ToSegment()
        {
            return new TextOutgoingSegment(new TextOutgoingSegmentData(message));
        }
    }

    extension(byte[] data)
    {
        public ImageOutgoingSegment ToImageSegment(string? summary = null, SubType subType = SubType.Normal)
        {
            return new ImageOutgoingSegment(
                new ImageOutgoingSegmentData(new MilkyUri($"base64://{Convert.ToBase64String(data)}"), summary,
                    subType));
        }
    }
}
