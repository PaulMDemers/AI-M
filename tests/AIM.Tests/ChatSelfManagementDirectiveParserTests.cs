using AIM.Core.Chat;

namespace AIM.Tests;

public sealed class ChatSelfManagementDirectiveParserTests
{
    [Fact]
    public void ExtractsManagementBlockAndLeavesVisibleContent()
    {
        var result = ChatSelfManagementDirectiveParser.Extract(
            """
            Got it.
            <aim-management>{"memories":[{"action":"remember","content":"Paul prefers compact status updates."}],"personality":{"status":"Memory tuned","systemPromptAppend":"Prefer concise status updates."}}</aim-management>
            """);

        Assert.Equal("Got it.", result.VisibleContent);
        var memory = Assert.Single(result.Directive.Memories);
        Assert.Equal("remember", memory.Action);
        Assert.Equal("Paul prefers compact status updates.", memory.Content);
        Assert.Equal("Memory tuned", result.Directive.Personality?.Status);
        Assert.Equal("Prefer concise status updates.", result.Directive.Personality?.SystemPromptAppend);
    }

    [Fact]
    public void InvalidManagementJsonIsHiddenButIgnored()
    {
        var result = ChatSelfManagementDirectiveParser.Extract(
            "Visible<aim-management>{not-json}</aim-management>");

        Assert.Equal("Visible", result.VisibleContent);
        Assert.False(result.Directive.HasChanges);
    }
}
