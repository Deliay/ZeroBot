using Microsoft.Extensions.Logging;
using ZeroBot.Core.Services;

namespace ZeroBot.Core.Test;

public class BotContextTest
{
    private readonly ILoggerFactory _factory = LoggerFactory.Create((_) => { });
    private (BotContext, TestBotService) CreateTestContext() => (new BotContext(_factory.CreateLogger<BotContext>()), new TestBotService());

    private async Task RegisterAndAssert(BotContext botContext, TestBotService botService)
    {
        Assert.False(botService.GetCurrentAccountAsyncCalled);
        Assert.Empty(botContext.BotServices);
        await botContext.RegisterBotAsync(botService);
        Assert.True(botService.GetCurrentAccountAsyncCalled);
        Assert.Single(botContext.BotServices);
    }
    
    [Fact]
    public async Task ShouldObtainAccountInfoAndRegister()
    {
        var (botContext, botService) = CreateTestContext();

        await RegisterAndAssert(botContext, botService);
    }

    [Fact]
    public async Task ShouldThrowWhenAddDuplicatedAccount()
    {
        var (botContext, botService) = CreateTestContext();

        await RegisterAndAssert(botContext, botService);
        
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await botContext.RegisterBotAsync(botService));
    }
    
    [Fact]
    public async Task ShouldUnregisterBot()
    {
        var (botContext, botService) = CreateTestContext();
        await RegisterAndAssert(botContext, botService);
        
        await botContext.UnregisterBot(botService);
        Assert.Empty(botContext.BotServices);
    }
}
