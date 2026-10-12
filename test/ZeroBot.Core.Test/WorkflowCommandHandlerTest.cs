using ZeroBot.Workflow;

namespace ZeroBot.Core.Test;

public class WorkflowCommandHandlerTest
{
    [Fact]
    public void TryParse_ShouldExtractSinkAndScript()
    {
        Assert.True(WorkflowCommandHandler.TryParse("/workflow:run:file:a -> b", out var sink, out var script));
        Assert.Equal("file", sink);
        Assert.Equal("a -> b", script);
    }

    [Fact]
    public void TryParse_ScriptShouldPreserveSeparatorsAndNewlines()
    {
        Assert.True(WorkflowCommandHandler.TryParse("/workflow:run:voice:line1\nline2:x", out var sink, out var script));
        Assert.Equal("voice", sink);
        Assert.Equal("line1\nline2:x", script);
    }

    [Fact]
    public void TryParse_ShouldSupportFullWidthSeparators()
    {
        Assert.True(WorkflowCommandHandler.TryParse("/workflow：run：file：print('hi')", out var sink, out var script));
        Assert.Equal("file", sink);
        Assert.Equal("print('hi')", script);
    }

    [Fact]
    public void TryParse_ShouldTrimOuterWhitespace()
    {
        Assert.True(WorkflowCommandHandler.TryParse("/workflow:run:  file  :  script  ", out var sink, out var script));
        Assert.Equal("file", sink);
        Assert.Equal("script", script);
    }

    [Fact]
    public void TryParse_SinkWithoutScriptShouldReturnTrueWithEmptyScript()
    {
        Assert.True(WorkflowCommandHandler.TryParse("/workflow:run:file", out var sink, out var script));
        Assert.Equal("file", sink);
        Assert.Equal(string.Empty, script);
    }

    [Theory]
    [InlineData("/workflow:run")]
    [InlineData("/workflow:run:file:")]
    [InlineData("/workflow:enable")]
    [InlineData("/workflow:disable")]
    [InlineData("/workflow")]
    [InlineData("")]
    public void TryParse_ShouldRejectInvalidShapes(string raw)
    {
        Assert.False(WorkflowCommandHandler.TryParse(raw, out _, out _));
    }

    [Theory]
    [InlineData("/workflow:run:file:x", true)]
    [InlineData("/workflow:run", true)]
    [InlineData("/workflow:run:voice", true)]
    [InlineData("/workflow：run：voice：x", true)]
    [InlineData("/workflow:enable", false)]
    [InlineData("/workflow", false)]
    [InlineData("/workflowx:run:file:x", false)]
    [InlineData("/other:run:file:x", false)]
    public void IsRunCommand_ShouldMatchOnlyRun(string raw, bool expected)
    {
        Assert.Equal(expected, WorkflowCommandHandler.IsRunCommand(raw));
    }
}
