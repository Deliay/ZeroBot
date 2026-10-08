using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milky.Net.Client;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Utility;

namespace ZeroBot.Workflow;

/// <summary>
/// 工作流指令：/workflow:run:{file|voice}:{script}
/// 仅群聊可用，且仅高权限用户（sudoers / 群管理员）可触发。
/// script 为指令第二个分隔符之后的全部内容（可换行）。
/// 执行流程：/v1/compile 校验展开 → /v1/run 执行 → 按 file/voice 发送结果；任一步失败均回复错误信息。
/// </summary>
public class WorkflowCommandHandler(
    ICommandDispatcher dispatcher,
    IBotContext bot,
    IPermission permission,
    IOptions<WorkflowOptions> options,
    WorkflowApi api,
    MilkyClient milky,
    ILogger<WorkflowCommandHandler> logger) : CommandQueuedHandler(dispatcher)
{
    private const string CommandPrefix = "/workflow";

    private static readonly char[] Separators = [':', '：'];

    private static readonly OutgoingSegment HelpStrings =
        ("/workflow:run:{file|voice}:{script}\n" +
         "示例：/workflow:run:voice:\n" +
         "print('hello')\n" +
         "script 可换行，会作为剩余全部内容执行。").ToMilkyTextSegment();

    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        if (message.Scene != MessageScene.Group) return false;
        if (!IsRunCommand(message.ToText().TrimStart())) return false;

        return await permission.IsSudoerOrGroupAdminAsync(bot, message, cancellationToken);
    }

    protected override async ValueTask DequeueAsync(Event<IncomingMessage> @event,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = @event.ToText().TrimStart();
            if (!TryParse(raw, out var sink, out var script))
            {
                await @event.ReplyAsGroup(bot, cancellationToken, [HelpStrings]);
                return;
            }

            if (!sink.Equals("file", StringComparison.OrdinalIgnoreCase)
                && !sink.Equals("voice", StringComparison.OrdinalIgnoreCase))
            {
                await @event.ReplyAsGroup(bot, cancellationToken,
                    ["未知的结果类型，仅支持 file 或 voice。".ToMilkyTextSegment()]);
                return;
            }

            if (string.IsNullOrWhiteSpace(script))
            {
                await @event.ReplyAsGroup(bot, cancellationToken, [HelpStrings]);
                return;
            }

            var value = options.Value;
            if (string.IsNullOrWhiteSpace(value.BaseUrl))
            {
                await @event.ReplyAsGroup(bot, cancellationToken,
                    ["工作流服务未配置（WorkflowService__BaseUrl）。".ToMilkyTextSegment()]);
                return;
            }

            var timeout = TimeSpan.FromSeconds(Math.Max(1, value.HttpTimeoutSeconds));

            var compile = await api.CompileAsync(value.BaseUrl, script, timeout, cancellationToken);
            if (!compile.Success)
            {
                await ReplyErrorAsync(@event, compile.Error, cancellationToken);
                return;
            }

            var run = await api.RunAsync(value.BaseUrl, script, timeout, cancellationToken);
            if (!run.Success || run.Data is null || run.Data.Length == 0)
            {
                await ReplyErrorAsync(@event, run.Error, cancellationToken);
                return;
            }

            await SendResultAsync(@event, sink, run.Data, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "处理工作流指令时发生异常");
            try
            {
                await @event.ReplyAsGroup(bot, cancellationToken,
                    ["工作流指令处理失败，请稍后重试。".ToMilkyTextSegment()]);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "发送工作流错误信息失败");
            }
        }
        finally
        {
            // 摘除表情必须兜底：finally 内异常会逃出串行消费循环，永久中断本处理器队列。
            try
            {
                await @event.RemoveReaction(bot, KnownReactionEmojiIds.Click, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "移除处理中表情失败");
            }
        }
    }

    protected override ValueTask EnqueueInspectorAsync(Event<IncomingMessage> @event,
        CancellationToken cancellationToken = default)
        => @event.AddReaction(bot, KnownReactionEmojiIds.Click, cancellationToken);

    private async ValueTask ReplyErrorAsync(Event<IncomingMessage> @event, string? error,
        CancellationToken cancellationToken)
    {
        var message = string.IsNullOrWhiteSpace(error) ? "工作流执行失败，请稍后重试。" : error;
        await @event.ReplyAsGroup(bot, cancellationToken, [$"⚠️ {message}".ToMilkyTextSegment()]);
    }

    private async ValueTask SendResultAsync(Event<IncomingMessage> @event, string sink, byte[] data,
        CancellationToken cancellationToken)
    {
        var base64 = Convert.ToBase64String(data);

        if (sink.Equals("voice", StringComparison.OrdinalIgnoreCase))
        {
            var record = new RecordOutgoingSegment(
                new RecordOutgoingSegmentData(new MilkyUri($"base64://{base64}")));
            await @event.SendAsGroup(bot, cancellationToken, new OutgoingSegment[] { record });
            return;
        }

        var fileName = GuessFileName(data);
        await milky.File.UploadGroupFileAsync(
            new UploadGroupFileInput(@event.Data.PeerId, "/", new MilkyUri($"base64://{base64}"), fileName),
            cancellationToken);
    }

    /// <summary>
    /// 匹配 <c>/workflow:run:...</c> 指令形状（不校验 result-sink 与 script）。
    /// </summary>
    private static bool IsRunCommand(string raw)
    {
        if (!raw.StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase)) return false;

        var rest = raw[CommandPrefix.Length..];
        if (rest.Length == 0 || !Separators.Contains(rest[0])) return false;

        var after = rest[1..];
        var index = after.IndexOfAny(Separators);
        var action = (index < 0 ? after : after[..index]).Trim();
        return action.Equals("run", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 解析 <c>/workflow:run:{sink}:{script}</c>，script 保留换行（仅修剪首尾空白）；
    /// script 可包含分隔符。形状不合法（如缺少 sink 分隔符）时返回 false。
    /// </summary>
    private static bool TryParse(string raw, out string sink, out string script)
    {
        sink = string.Empty;
        script = string.Empty;

        if (!raw.StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase)) return false;

        var rest = raw[CommandPrefix.Length..];
        if (rest.Length == 0 || !Separators.Contains(rest[0])) return false;

        // 跳过 "/workflow:" 与 action，定位 result-sink。
        rest = rest[1..];
        var actionSeparator = rest.IndexOfAny(Separators);
        if (actionSeparator < 0) return false;

        rest = rest[(actionSeparator + 1)..];
        var scriptSeparator = rest.IndexOfAny(Separators);
        if (scriptSeparator < 0)
        {
            // 只有 result-sink、缺少 script 分隔符：sink 有效但 script 为空。
            sink = rest.Trim();
            return sink.Length > 0;
        }

        sink = rest[..scriptSeparator].Trim();
        script = rest[(scriptSeparator + 1)..].Trim();
        return sink.Length > 0 && script.Length > 0;
    }

    /// <summary>
    /// 按文件魔数推断扩展名，构造带时间戳的文件名；未知类型回落到 .bin。
    /// </summary>
    private static string GuessFileName(byte[] data)
    {
        var extension = DetectExtension(data);
        return $"workflow-{DateTimeOffset.Now:yyyyMMddHHmmss}.{extension}";
    }

    private static string DetectExtension(byte[] data)
    {
        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
            && data[8] == 0x57 && data[9] == 0x41 && data[10] == 0x56 && data[11] == 0x45)
            return "wav";

        if (data.Length >= 4 && data[0] == 0x4F && data[1] == 0x67 && data[2] == 0x67 && data[3] == 0x53)
            return "ogg";

        if (data.Length >= 4 && data[0] == 0x66 && data[1] == 0x4C && data[2] == 0x61 && data[3] == 0x43)
            return "flac";

        if (data.Length >= 3 && data[0] == 0x49 && data[1] == 0x44 && data[2] == 0x33)
            return "mp3";

        if (data.Length >= 2 && data[0] == 0xFF && (data[1] & 0xE0) == 0xE0)
            return "mp3";

        if (data.Length >= 12 && data[4] == 0x66 && data[5] == 0x74 && data[6] == 0x79 && data[7] == 0x70)
            return "mp4";

        if (data.Length >= 5 && data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46)
            return "pdf";

        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "png";

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "jpg";

        if (data.Length >= 4 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
            return "gif";

        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
            && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return "webp";

        if (data.Length >= 4 && data[0] == 0x50 && data[1] == 0x4B && data[2] == 0x03 && data[3] == 0x04)
            return "zip";

        return "bin";
    }
}
