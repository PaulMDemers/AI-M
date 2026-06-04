using AIM.Core.Tools;

namespace AIM.Tests;

public sealed class AgentToolRequestParserTests
{
    [Fact]
    public void ExtractsToolBlockAndLeavesVisibleContent()
    {
        var result = AgentToolRequestParser.Extract(
            """
            Let me check.
            <aim-tools>{"calls":[{"id":"one","name":"memory.list","arguments":{}}]}</aim-tools>
            """);

        Assert.Equal("Let me check.", result.VisibleContent);
        var call = Assert.Single(result.Request.Calls);
        Assert.Equal("one", call.Id);
        Assert.Equal("memory.list", call.Name);
    }

    [Fact]
    public void InvalidToolJsonIsHiddenButIgnored()
    {
        var result = AgentToolRequestParser.Extract("Visible<aim-tools>{nope}</aim-tools>");

        Assert.Equal("Visible", result.VisibleContent);
        Assert.False(result.Request.HasCalls);
    }
}
