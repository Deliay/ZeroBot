using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using TinyPinyin;
using ZeroBot.Abstraction.Bot;
using ZeroBot.TestPlugin.Config;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.TestPlugin;

public class Boots(IBotContext bot, ILogger<Boots> logger, IJsonConfig<BootsConfig> config) : MessageQueueHandler<Boots>(bot, logger) 
{
    private Dictionary<string, string> _questions = [];
    private readonly IBotContext _bot = bot;
    private readonly ILogger<Boots> _logger = logger;

    private void InitializeQuestions()
    {
        var path = Path.GetFullPath(config.Current.questionDir);
        _questions = Directory
            .EnumerateFiles(path, "*.png")
            .ToDictionary((p) => Path.GetFileNameWithoutExtension(p)!, Path.GetFullPath);
        
        _logger.LogInformation("{count} questions loaded from {path}.",  _questions.Count, path);
    }
    
    protected override async ValueTask InitializeHandler(CancellationToken cancellationToken = default)
    {
        InitializeQuestions();
        await config.WaitForInitializedAsync(cancellationToken);
        await base.InitializeHandler(cancellationToken);
    }

    private async ValueTask StartNewQuestionAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        var groupId = @event.Data.PeerId;
        // 先看当前有没有题，有的话快速回复
        if (config.Current.groupBoots.TryGetValue(groupId, out var groupSnapshot)
            && groupSnapshot.currentQuestion is not null)
        {
            var questedAt = groupSnapshot.questionRecords[groupSnapshot.currentQuestion].questedAt;
            // 看是否过了一个小时
            if (DateTimeOffset.Now - questedAt <= TimeSpan.FromHours(1))
            {
                var messageId = groupSnapshot.questionRecords[groupSnapshot.currentQuestion].messageId;
                await @event.SendAsGroup(_bot, cancellationToken, [
                    messageId.ReplyAsMessage(),
                    @event.Data.SenderId.MentionAsUser(),
                    $"本群已经开启了谐音梗挑战，请完成这个挑战之后再出新的题~".ToMilkyTextSegment(),
                ]);
                return;
            }
            // 清理掉当前问题，然后就当没有题了
            await config.BeginConfigMutationScopeAsync(async (current, token) =>
            {
                var data = current.groupBoots[groupId];
                data.questionRecords.Remove(data.currentQuestion!);
                current.groupBoots[groupId] = data with
                {
                    currentQuestion = null,
                };
                await config.SaveAsync(current, token);
            }, cancellationToken);
        }
        // 没有题的话，生成一个题目
        var (success, question) = await config.BeginConfigMutationScopeAsync<(bool, string?)>(async (current, token) =>
        {
            if (!current.groupBoots.TryGetValue(groupId, out var data))
                current.groupBoots.Add(groupId, data = GroupBoots.Create(groupId));

            var historyQuestions = data.questionRecords.Keys;
            var newQuestion = _questions.Keys
                .Where((q) => !historyQuestions.Contains(q))
                .Shuffle()
                .FirstOrDefault();

            if (newQuestion is null) return (false, null!);
            var messageId = @event.Data.MessageSeq;
            data.questionRecords.Add(newQuestion, new BootsTest(messageId, DateTimeOffset.Now));
            current.groupBoots[groupId] = data with
            {
                currentQuestion = newQuestion,
            };
            await config.SaveAsync(current, token);
            return (true, newQuestion);
        }, cancellationToken);

        if (!success || question is null or { Length: 0 })
        {
            await @event.ReplyAsGroup(_bot, cancellationToken, [
                "题库里的题已经全都玩过啦，暂时没有新的题目，请等待题库更新".ToMilkyTextSegment()
            ]);
            return;
        }
        
        var path = _questions[question!];
        var hint = $"题目已经生成~快来作答吧，答案字数：{question.Length}\n" +
                   "直接回答你的猜想即可，不要带其他内容~\n\n" +
                   "注意：\n" +
                   "1. 在这个题目解决之前，无法生成新的题目。" +
                   "2. 题目有效期1小时~" +
                   "3. 如果回答匹配（拼音匹配）则回答正确~";

        var uri = (new UriBuilder()
        {
            Scheme = Uri.UriSchemeFile,
            Host = "",
            Path = path,
        }).Uri.AbsoluteUri;
        _logger.LogInformation("Sending final question {question}, path={path}", question, uri);
        await @event.ReplyAsGroup(_bot, cancellationToken, [
            uri.ToMilkyImageSegment(),
            hint.ToMilkyTextSegment()
        ]);
    }

    private async ValueTask TryValidateAnswerAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        var groupId = @event.Data.PeerId;
        // 当前没有这个群的配置，则直接跳过
        if (!config.Current.groupBoots.TryGetValue(groupId, out var groupSnapshot)) return;

        var (isCorrect, question) = await config.BeginConfigMutationScopeAsync<(bool, string?)>(async (current, token) =>
        {
            if (!current.groupBoots.TryGetValue(groupId, out var data))
                current.groupBoots.Add(groupId, data = GroupBoots.Create(groupId));

            // 当前有题，看题目有没有被解决，没有就跳过
            if (data.currentQuestion is null) return (false, null!);

            var quest = data.questionRecords[data.currentQuestion];
            // 已被解决，跳过
            if (quest.isResolved) return (false, null!);

            // 先看长度是否一致，再看拼音是否一致
            var text = @event.Data.ToText().Trim();
            if (text.Length != data.currentQuestion.Length
                || PinyinHelper.GetPinyin(text) != PinyinHelper.GetPinyin(data.currentQuestion)) return (false, null!);

            // 标记为问题已解决
            data.questionRecords[data.currentQuestion] = quest with
            {
                isResolved = true,
                resolvedAt = DateTimeOffset.Now,
            };
            current.groupBoots[groupId] = data with
            {
                currentQuestion = null,
            };
            // 保存
            await config.SaveAsync(current, token);
            return (true, data.currentQuestion);
        }, cancellationToken);

        if (isCorrect)
        {
            await @event.ReplyAsGroup(_bot, cancellationToken, [
                $"恭喜你，回答正确 🎉 正确答案是{question}".ToMilkyTextSegment(),
            ]);
        }
    }
    
    protected override ValueTask DequeueAsync(Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        if (_questions.Count == 0) return ValueTask.CompletedTask;

        var atBot = @event.Data.Segments
            .OfType<MentionIncomingSegment>()
            .Any(seg => seg.Data.UserId == @event.SelfId);
        var text = @event.Data.ToText().Trim();
        if (atBot && text.StartsWith("/谐音梗挑战"))
        {
            return StartNewQuestionAsync(@event, cancellationToken);
        }
        
        return TryValidateAnswerAsync(@event, cancellationToken);
    }
}