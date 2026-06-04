using AIM.Core.Chat;

namespace AIM.Tests;

public sealed class HiddenMarkupDisplaySanitizerTests
{
    [Fact]
    public void RemovesCompleteToolBlocks()
    {
        var content = HiddenMarkupDisplaySanitizer.Sanitize(
            "Let me check.<aim-tools>{\"calls\":[]}</aim-tools>Done.");

        Assert.Equal("Let me check.Done.", content);
    }

    [Fact]
    public void HidesIncompleteToolBlocksWhileStreaming()
    {
        var content = HiddenMarkupDisplaySanitizer.Sanitize("Let me check.<aim-tools>{\"calls\"");

        Assert.Equal("Let me check.", content);
    }

    [Fact]
    public void HidesIncompleteManagementBlocksWhileStreaming()
    {
        var content = HiddenMarkupDisplaySanitizer.Sanitize("Updated.<aim-management>{\"memories\"");

        Assert.Equal("Updated.", content);
    }
}
