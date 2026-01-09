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
        public IEnumerable<ITextCommand> ToTextCommands(char prefix = '/', string argumentSplitters = ":：-")
        {
            return message.Data.ToTextCommands(prefix, argumentSplitters);
        }

        public string ToText(string separator = "")
        {
            return message.Data.ToText(separator);
        }
        
        public async ValueTask SendAsGroup(IBotContext bot,
            CancellationToken cancellationToken = default,
            params OutgoingSegment[] segments)
        {
            await bot.WriteManyGroupMessageAsync(message.SelfId, [message.Data.PeerId], cancellationToken,
                segments);
        }
        
        public async ValueTask ReplyAsGroup(IBotContext bot,
            CancellationToken cancellationToken = default,
            params OutgoingSegment[] segments)
        {
            await message.SendAsGroup(bot, cancellationToken, [message.Data.Reply(), ..segments]);
        }
        
        public MessageScene Scene => message.Data switch
        {
            GroupIncomingMessage => MessageScene.Group,
            FriendIncomingMessage => MessageScene.Friend,
            TempIncomingMessage => MessageScene.Temp,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        public async IAsyncEnumerable<ImageIncomingSegment> GetMilkyImageMessagesAsync(IBotContext bot,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {

            if (message.Data.Segments.OfType<ReplyIncomingSegment>().FirstOrDefault() is { } reply)
            {
                Event<IncomingMessage>? replyMessage = null;
                if (bot.EventRepository is not null)
                {
                    replyMessage = await bot.EventRepository.SearchEventAsync<IncomingMessage>(message.SelfId, msg =>
                            msg.Data.PeerId == message.Data.PeerId
                            && msg.Data.MessageSeq == reply.Data.MessageSeq, cancellationToken)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                replyMessage ??= await bot.GetHistoryMessageAsync(message.SelfId, message.Scene, message.Data.PeerId,
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
        public TextOutgoingSegment ToMilkyTextSegment()
        {
            return new TextOutgoingSegment(new TextOutgoingSegmentData(message));
        }

        public ImageOutgoingSegment ToMilkyImageSegment()
        {
            return new ImageOutgoingSegment(new ImageOutgoingSegmentData(new MilkyUri(message), null, SubType.Normal));
        }
    }

    extension(long longId)
    {
        public ReplyOutgoingSegment ReplyAsMessage()
        {
            return new ReplyOutgoingSegment(new ReplyOutgoingSegmentData(longId));
        }

        public MentionOutgoingSegment MentionAsUser()
        {
            return new MentionOutgoingSegment(new MentionOutgoingSegmentData(longId));
        }
    }

    extension(byte[] data)
    {
        public ImageOutgoingSegment ToMilkyImageSegment(string? summary = null, SubType subType = SubType.Normal)
        {
            return new ImageOutgoingSegment(
                new ImageOutgoingSegmentData(new MilkyUri($"base64://{Convert.ToBase64String(data)}"), summary,
                    subType));
        }
    }

    private static readonly HttpClient ImageRequestHttpClient = new();
    extension(ImageIncomingSegment message)
    {
        public async ValueTask<string> GetMilkyImageUrlAsync(IBotContext bot, Event @event,
            CancellationToken cancellationToken = default)
        {
            if (DateTimeOffset.UtcNow - @event.Time < TimeSpan.FromHours(1))
            {
                return message.Data.TempUrl;
            }
            return await bot.GetTempResourceUrlAsync(@event.SelfId, message.Data.ResourceId, cancellationToken);
        }

        public async ValueTask<Stream> GetMilkyImageStreamAsync(IBotContext bot, Event @event,
            CancellationToken cancellationToken = default)
        {
            var imageUrlAsync = await message.GetMilkyImageUrlAsync(bot, @event, cancellationToken);
            return await ImageRequestHttpClient.GetStreamAsync(
                imageUrlAsync, cancellationToken);
        }

        public async ValueTask<byte[]> GetMilkyImageBytesAsync(IBotContext bot, Event @event,
            CancellationToken cancellationToken = default)
        {
            var imageUrlAsync = await message.GetMilkyImageUrlAsync(bot, @event, cancellationToken);
            return await ImageRequestHttpClient.GetByteArrayAsync(
                imageUrlAsync, cancellationToken);
        }
    }
}
