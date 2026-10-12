using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;

namespace ZeroBot.Painter;

/// <summary>
/// 绘图与公共素材指令共用的图片收集。
/// 刻意不复用 <c>GetMilkyImageMessagesAsync</c>（该扩展对每条消息只取首图），
/// 而是枚举全部 <see cref="ImageIncomingSegment"/>，并保留「引用消息的图片在前」的优先级。
/// </summary>
internal static class PainterImages
{
    /// <summary>
    /// 被引用消息中的全部图片（按出现顺序）；无引用、引用消息已过期或其中没有图片时返回空列表。
    /// </summary>
    public static async ValueTask<List<byte[]>> CollectReferencedImagesAsync(IBotContext bot,
        Event<IncomingMessage> @event, int max, CancellationToken cancellationToken)
    {
        var images = new List<byte[]>();
        if (max <= 0) return images;
        if (@event.Data.Segments.OfType<ReplyIncomingSegment>().FirstOrDefault() is not { } reply) return images;

        Event<IncomingMessage>? referenced = null;
        if (bot.EventRepository is not null)
        {
            referenced = await bot.EventRepository.SearchEventAsync<IncomingMessage>(@event.SelfId,
                    msg => msg.Data.PeerId == @event.Data.PeerId
                           && msg.Data.MessageSeq == reply.Data.MessageSeq, cancellationToken)
                .FirstOrDefaultAsync(cancellationToken);
        }

        referenced ??= await bot.GetHistoryMessageAsync(@event.SelfId, @event.Scene, @event.Data.PeerId,
            reply.Data.MessageSeq, cancellationToken);

        if (referenced is null) return images;

        // 用被引用消息自身的事件取图：TempUrl 的有效期按被引用消息的时间判断，避免用当前消息的时间误判为新鲜。
        foreach (var segment in referenced.Data.Segments.OfType<ImageIncomingSegment>())
        {
            if (images.Count >= max) break;
            images.Add(await segment.GetMilkyImageBytesAsync(bot, referenced, cancellationToken));
        }

        return images;
    }

    /// <summary>
    /// 本条消息上下文中的图片：引用消息的图片在前、本消息的图片在后，最多 <paramref name="max"/> 张。
    /// </summary>
    public static async ValueTask<List<byte[]>> CollectContextImagesAsync(IBotContext bot,
        Event<IncomingMessage> @event, int max, CancellationToken cancellationToken)
    {
        var images = await CollectReferencedImagesAsync(bot, @event, max, cancellationToken);
        if (max <= 0) return images;

        foreach (var segment in @event.Data.Segments.OfType<ImageIncomingSegment>())
        {
            if (images.Count >= max) break;
            images.Add(await segment.GetMilkyImageBytesAsync(bot, @event, cancellationToken));
        }

        return images;
    }
}
