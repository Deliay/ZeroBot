using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;

namespace ZeroBot.Workflow;

/// <summary>
/// 工作流输入解析：从被引用消息（或本条消息）中取出群文件，并下载其内容作为工作流二进制输入。
/// </summary>
internal static class WorkflowFileInput
{
    /// <summary>
    /// 群文件注入工作流时使用的输入名，对应脚本中的 <c>[input]</c>（等价于 <c>[input:input]</c>）。
    /// </summary>
    public const string InputName = "input";

    // 下载链接由协议端签发，直接 GET 即可；实例级超时置为无限，唯一超时来源是调用方的取消令牌。
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>
    /// 找到被引用消息（或本条消息）中的群文件消息段；没有引用、引用消息已过期或其中没有文件时返回 null。
    /// 引用消息中的文件优先于本条消息中的文件。
    /// </summary>
    public static async ValueTask<FileIncomingSegment?> FindAsync(IBotContext bot,
        Event<IncomingMessage> @event, CancellationToken cancellationToken)
    {
        if (@event.Data.Segments.OfType<ReplyIncomingSegment>().FirstOrDefault() is { } reply)
        {
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

            if (referenced?.Data.Segments.OfType<FileIncomingSegment>().FirstOrDefault() is { } referencedFile)
            {
                return referencedFile;
            }
        }

        return @event.Data.Segments.OfType<FileIncomingSegment>().FirstOrDefault();
    }

    /// <summary>
    /// 下载群文件内容；协议端未返回下载链接时返回 null。
    /// </summary>
    public static async ValueTask<byte[]?> DownloadAsync(IBotContext bot, Event<IncomingMessage> @event,
        FileIncomingSegment segment, CancellationToken cancellationToken)
    {
        var url = await bot.GetGroupFileDownloadUrlAsync(@event.SelfId, @event.Data.PeerId, segment.Data.FileId,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(url)) return null;

        return await Http.GetByteArrayAsync(url, cancellationToken);
    }
}
