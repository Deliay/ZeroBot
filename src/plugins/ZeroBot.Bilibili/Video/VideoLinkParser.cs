using System.IO.Compression;
using System.Threading.Channels;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Mikibot.Crawler.Http.Bilibili;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;

namespace ZeroBot.Bilibili.Video;

public class VideoLinkParser(
    IPermission permission,
    IBotContext bot,
    HttpClient client,
    BiliVideoCrawler crawler,
    ILogger<VideoLinkParser> logger) : MessageQueueHandler(bot)
{
    private readonly Channel<Event<IncomingMessage>> _processQueue = Channel.CreateUnbounded<Event<IncomingMessage>>();
    private readonly IBotContext _bot = bot;

    private static readonly HashSet<char> ValidBv = [
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
        'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
        'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
        '1', '2', '3', '4', '5', '6', '7', '8', '9', '0'
    ];
    private static readonly HashSet<char> ValidAv = [
        '1', '2', '3', '4', '5', '6', '7', '8', '9', '0'
    ];
    private static string Fetch(string raw, int startIndex, HashSet<char> allow)
    {
        for (var i = startIndex + 1; i < raw.Length; i++)
        {
            if (!allow.Contains(raw[i]))
                return raw[startIndex..i];
        }
        return raw[startIndex..];
    }
    private static readonly List<string> Selectors =
    [
        "//meta[@property='og:image']",
        "//meta[@property='og:url']",
        "//meta[@name='title']",
        "//meta[@name='author']"
    ];

    private const string B23Raw = "https://b23.tv/";
    private const string B2233Raw = "https://bili2233.cn/";
    private async ValueTask TrySend(Event<IncomingMessage> @event, string? bv, string? av, CancellationToken cancellationToken)
    {
        var result = await crawler.GetVideoInfo(bv, av == null ? null : int.Parse(av!), cancellationToken);

        await @event.ReplyAsGroup(_bot, cancellationToken,
            [
                result.CoverUrl.ToMilkyImageSegment(),
                $"{result.Title} (作者: {result.Owner.Name}) \n https://bilibili.com/{result.BvId}".ToMilkyTextSegment()
            ]);
    }
    
    private async ValueTask TrySend(Event<IncomingMessage> @event, string url, CancellationToken cancellationToken = default)
    {
        var doc = new HtmlDocument();
        await using var contentStream = await client.GetStreamAsync(url, cancellationToken);
        await using var uncompressedStream = new GZipStream(contentStream, CompressionMode.Decompress);
        doc.Load(uncompressedStream);

        var infoList = Selectors
            .Select(s => doc.DocumentNode.SelectSingleNode(s))
            .Select(s => s.Attributes["content"].Value)
            .ToList();

        var image = "https:" + infoList[0];
        var bvUrl = infoList[1];
        var title = infoList[2];
        var author = infoList[3];
        var atImage = image.IndexOf('@', StringComparison.InvariantCulture);
        var fullImage = atImage > 0 ? image[..atImage] : image;
        
        await @event.ReplyAsGroup(_bot, cancellationToken, 
            [
                fullImage.ToMilkyImageSegment(),
                $"{title} (作者: {author}) \n {bvUrl}".ToMilkyTextSegment()
            ]);
    }

    private ValueTask ProcessAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        var text = @event.Data.ToText();
        var bvStart = text.IndexOf("/BV", StringComparison.InvariantCulture);
        if (bvStart > -1)
        {
            var bv = Fetch(text, bvStart + 1, ValidBv);
            return TrySend(@event, bv, null, cancellationToken);
        }
        
        var avStart = text.IndexOf("/av", StringComparison.InvariantCulture);
        if (avStart > -1)
        {
            var av = Fetch(text, avStart + 3, ValidAv);
            return TrySend(@event, null, av, cancellationToken);
        }
        
        var b23 = text.IndexOf(B23Raw, StringComparison.InvariantCulture);
        var b2233 = text.IndexOf(B2233Raw, StringComparison.InvariantCulture);
        if (b23 <= -1 && b2233 <= -1) return default;
        
        var pos = b23 > -1 ? b23 : b2233;
        var prefix = b23 > -1 ? B23Raw : B2233Raw;
        var suffix = Fetch(text, pos + prefix.Length, ValidBv);
        var url = prefix + suffix;
        return TrySend(@event, url, cancellationToken);
    }

    private async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        if (message.Data.Segments.Any(seg => seg is not TextIncomingSegment)) return false;
        var text = message.Data.ToText();
        var validMessage = (text.IndexOf("/BV", StringComparison.InvariantCulture) > -1
                            || text.IndexOf("/av", StringComparison.InvariantCulture) > -1
                            || text.IndexOf(B23Raw, StringComparison.InvariantCulture) > -1
                            || text.IndexOf(B2233Raw, StringComparison.InvariantCulture) > -1);
        return validMessage
               && await permission.CheckGroupPermissionAsync(message.Data.PeerId, "bilibili.parser", cancellationToken);
    }

    protected override async ValueTask DequeueAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        if (!await PredicateAsync(@event, cancellationToken)) return;

        try
        {
            await ProcessAsync(@event, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while processing incoming message");
        }
    }
}