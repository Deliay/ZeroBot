using ZeroBot.Abstraction.Bot;
using ZeroBot.Core.Services.Commands;

namespace ZeroBot.Core.Test;

public class CommandInvokerTest
{
    
    [Fact]
    public void ShouldGenerateConvertMethodCorrectly()
    {
        var methodCalled = false;
        var handler = IncomingCommandExtensions.GetInvoker(TestSyncMethod);
        string[] commandArgs = ["1", "2", "abc"];
        handler(commandArgs);
        Assert.True(methodCalled);
        
        return;
        void TestSyncMethod(int a, long b, string c)
        {
            Assert.Equal(1, a);
            Assert.Equal(2, b);
            Assert.Equal("abc", c);
            methodCalled = true;
        }
    }

    [Fact]
    public async Task ShouldGenerateAsyncConvertMethodCorrectly()
    {
        var methodCalled = false;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var token = cts.Token;
        var handler = IncomingCommandExtensions.GetAsyncInvoker(TestSyncMethod);
        string[] commandArgs = ["1", "2", "abc"];
        await handler(commandArgs, token);
        Assert.True(methodCalled);
        
        return;
        Task TestSyncMethod(int a, long b, string c, CancellationToken cancellationToken)
        {
            Assert.Equal(1, a);
            Assert.Equal(2, b);
            Assert.Equal("abc", c);
            Assert.Equal(cancellationToken, token);
            Assert.True(cancellationToken.IsCancellationRequested);
            methodCalled = true;
            return Task.CompletedTask;
        }
    }

    private static IncomingCommandParser Parser = new('/', [':']);
    private static IIncomingCommand Command = Parser.Parse("/test:1:2:abc").First();
    
    [Fact]
    public void ShouldThrowWhenArgumentCountNotMatch()
    {
        Assert.Throws<InvalidOperationException>(() => Command.InvokeCommand(InvalidTestMethod));
        return;
        void InvalidTestMethod(int a, long b) {}
    }
    [Fact]
    public void ShouldInvokeHandlerCorrectly()
    {
        var methodCalled = false;
        Command.InvokeCommand(ValidTestMethod);
        Assert.True(methodCalled);
        
        var methodCalledRet = false;
        Command.InvokeCommand(ValidTestMethodWithReturnValue);
        Assert.True(methodCalledRet);
        return;

        void ValidTestMethod(int a, long b, string c)
        {
            Assert.Equal(1, a);
            Assert.Equal(2, b);
            Assert.Equal("abc", c);
            methodCalled = true;
        }

        int ValidTestMethodWithReturnValue(int a, long b, string c)
        {
            Assert.Equal(1, a);
            Assert.Equal(2, b);
            Assert.Equal("abc", c);
            methodCalledRet = true;
            return a;
        }
    }

    [Fact]
    public async Task ShouldThrowWhenArgumentCountNotMatchWhenInvokeAsync()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Command.InvokeCommandAsync(InvalidTestMethod));
        return;
        Task InvalidTestMethod(int a, long b, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public async Task ShouldThrowWhenReturningTypeNotTaskWhenInvokeAsync()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Command.InvokeCommandAsync(InvalidTestMethod));
        return;
        Task<int> InvalidTestMethod(int a, long b, CancellationToken cancellationToken) => Task.FromResult(a);
    }
    [Fact]
    public void ShouldInvokeHandlerAsyncCorrectly()
    {
        var methodCalled = false;
        Command.InvokeCommandAsync(ValidTestMethodAsync);
        Assert.True(methodCalled);
        return;

        Task ValidTestMethodAsync(int a, long b, string c, CancellationToken cancellationToken)
        {
            Assert.Equal(1, a);
            Assert.Equal(2, b);
            Assert.Equal("abc", c);
            methodCalled = true;
            return Task.CompletedTask;
        }
    }
}