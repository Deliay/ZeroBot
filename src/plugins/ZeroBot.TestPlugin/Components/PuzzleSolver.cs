using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;
using ZeroBot.TestPlugin.Config;
using ZeroBot.Utility;
using ZeroBot.Utility.FileWatcher;

namespace ZeroBot.TestPlugin.Components;

public readonly record struct Puzzle(int Rows, int Cols, int[][] Obstacles)
{
    public int[][] PuzzleArray()
    {
        var array = new int[Rows][];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = new int[Cols];
        }

        foreach (var pos in Obstacles)
        {
            var x = pos[0];
            var y = pos[1];
            array[x][y] = -1;
        }
        return array;
    }

    private const string EmojiIndexes = "1️⃣2️⃣3️⃣4️⃣5️⃣6️⃣7️⃣8️⃣9️⃣";

    public static string ToEmoji(int[][] arrayPuzzle)
    {
        StringBuilder sb = new();
        foreach (var row in arrayPuzzle)
        {
            foreach (var col in row)
            {
                sb.Append(col switch
                {
                    -1 => "🈲",
                    _ => EmojiIndexes[col]
                });
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }
}

public readonly record struct Solve(int Id, int[] Anchor, int[][] Shape)
{
    public void PutTo(int[][] puzzle)
    {
        var x = Anchor[0];
        var y = Anchor[1];
        
        foreach (var point in Shape)
        {
            Shape[x + point[0]][y + point[1]] = Id;
        }
    }
}

public class PuzzleSolver(
    IBotContext bot,
    ICommandDispatcher dispatcher,
    IJsonConfig<PuzzleSolverConfig> config) : CommandQueuedHandler(dispatcher)
{
    private static readonly MediaTypeHeaderValue JsonContentType = new("application/json");
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri(config.Current.Endpoint),
    };
    
    protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        return message.ToText().Trim() == "/解题"
            && (await message.GetMilkyImageMessagesAsync(bot, cancellationToken).AnyAsync(cancellationToken));
    }

    private async ValueTask<string> ParseAsync(Event<IncomingMessage> @event,
        CancellationToken cancellationToken = default)
    {
        var image = await @event.GetMilkyImageMessagesAsync(bot, cancellationToken)
            .FirstOrDefaultAsync(cancellationToken);
        if (image is null) throw new InvalidOperationException("必须发送或引用一张包含棋盘和方块的图片作为输入");
            
        var imageData = await image.GetMilkyImageBytesAsync(bot, @event, cancellationToken);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(imageData), "file", "image.png");
        var parseResult = await _httpClient.PostAsync("/parse", content, cancellationToken);
        parseResult.EnsureSuccessStatusCode();
        return await parseResult.Content.ReadAsStringAsync(cancellationToken);
    }
    
    private async IAsyncEnumerable<Solve> SolveAsync(string question,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var body = new StringContent(question, JsonContentType);
        var solveResult = await _httpClient.PostAsync("/solve", body, cancellationToken);
            
        solveResult.EnsureSuccessStatusCode();
        await foreach (var solve in solveResult.Content.ReadFromJsonAsAsyncEnumerable<Solve>(cancellationToken))
        {
            yield return solve;
        }
    }

    protected override async ValueTask DequeueAsync(Event<IncomingMessage> @event,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var question = await ParseAsync(@event, cancellationToken);
            var puzzle = JsonSerializer.Deserialize<Puzzle>(question).PuzzleArray();
            await foreach (var solve in SolveAsync(question, cancellationToken))
            {
                solve.PutTo(puzzle);
            }
            
            await @event.ReplyAsGroup(bot, cancellationToken, [Puzzle.ToEmoji(puzzle).ToMilkyTextSegment()]);
        }
        catch (Exception ex)
        {
            await @event.ReplyAsGroup(bot, cancellationToken, [$"解题失败！{ex.Message}".ToMilkyTextSegment()]);
        }
    }
}
