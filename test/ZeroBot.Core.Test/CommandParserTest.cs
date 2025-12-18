using ZeroBot.Core.Services.Commands;

namespace ZeroBot.Core.Test;

public class CommandParserTest
{
    private static readonly TextCommandParser DefaultParser = new('/', [':', '：', '-']);
    
    [Fact]
    public void ShouldNotParseEmptyCommand()
    {
        var result = DefaultParser.Parse("test").ToList();
        Assert.Empty(result);
    }
    
    [Fact]
    public void ShouldParseSingleCommand()
    {
        var result = DefaultParser.Parse("/test").ToList();
        Assert.Single(result);
        var command = result.First();
        Assert.Equal("test", command.Name);
    }
    
    [Fact]
    public void ShouldParseMultiCommand()
    {
        var result = DefaultParser.Parse("/test1 /test2").ToList();
        Assert.Equal(2, result.Count);
        var firstCommand = result.First();
        Assert.Equal("test1 ", firstCommand.Name);
        var secondCommand = result.Last();
        Assert.Equal("test2", secondCommand.Name);
    }

    [Fact]
    public void ShouldParseCommandWithArgument()
    {
        var result = DefaultParser.Parse("/test:arg1:arg2：arg3-arg4").ToList();
        Assert.Single(result);
        var command = result.First();
        Assert.Equal("test", command.Name);
        Assert.Equal("arg1", command.ParseNextArgument<string>());
        Assert.Equal("arg2", command.ParseNextArgument<string>());
        Assert.Equal("arg3", command.ParseNextArgument<string>());
        Assert.Equal("arg4", command.ParseNextArgument<string>());
    }
    
    [Fact]
    public void ShouldParseMultipleCommandWithArgument()
    {
        var result = DefaultParser.Parse("/test1:arg1 /test2:arg2").ToList();
        Assert.Equal(2, result.Count);
        var firstCommand = result.First();
        Assert.Equal("test1", firstCommand.Name);
        Assert.Equal("arg1 ", firstCommand.ParseNextArgument<string>());
        var secondCommand = result.Last();
        Assert.Equal("test2", secondCommand.Name);
        Assert.Equal("arg2", secondCommand.ParseNextArgument<string>());
    }
    
    [Fact]
    public void ShouldReadArgumentWithIParseable()
    {
        var result = DefaultParser.Parse("/test:123").ToList();
        Assert.Single(result);
        var command = result.First();
        Assert.Equal("test", command.Name);
        Assert.Equal(123, command.ParseNextArgument<int>());
    }
}