using Milky.Net.Model;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Core.Test;

internal class TestBotService : IBotService
{
    public bool RunAsyncCalled { get; set; }
    public bool GetCurrentAccountAsyncCalled { get; set; }
    public bool SendGroupMessageAsyncCalled { get; set; }
    public static readonly GetLoginInfoOutput FakeAccount = new(123456, "test");
    public static readonly MultiGroupSendResult FakeSendResult = new()
    {
        { 123, new SendGroupMessageOutput(123456, DateTimeOffset.MinValue) },
    };
    
    public ValueTask RunAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        RunAsyncCalled = true;
        return default;
    }

    public ValueTask<GetLoginInfoOutput> GetCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        GetCurrentAccountAsyncCalled = true;
        return ValueTask.FromResult(FakeAccount);
    }

    public ValueTask<MultiGroupSendResult> SendGroupMessageAsync(HashSet<long> groupIds, CancellationToken cancellationToken = default,
        params OutgoingSegment[] messageSegments)
    {
        SendGroupMessageAsyncCalled = true;
        return ValueTask.FromResult(FakeSendResult);
    }
}