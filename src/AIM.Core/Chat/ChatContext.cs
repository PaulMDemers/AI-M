using AIM.Core.Memory;
using AIM.Core.Tools;

namespace AIM.Core.Chat;

public sealed class ChatContext
{
    public ChatContext(
        IReadOnlyList<MemoryRecord> memories,
        string selfManagementInstructions = "",
        string toolInstructions = "",
        IReadOnlyList<AgentToolDefinition>? toolDefinitions = null,
        string conversationSummary = "",
        DateTimeOffset? conversationSummaryUpdatedAt = null,
        string conversationSummaryInstructions = "")
    {
        Memories = memories;
        SelfManagementInstructions = selfManagementInstructions;
        ToolInstructions = toolInstructions;
        ToolDefinitions = toolDefinitions ?? [];
        ConversationSummary = conversationSummary;
        ConversationSummaryUpdatedAt = conversationSummaryUpdatedAt;
        ConversationSummaryInstructions = conversationSummaryInstructions;
    }

    public static ChatContext Empty { get; } = new([], ChatSelfManagementInstructions.Text);

    public IReadOnlyList<MemoryRecord> Memories { get; }

    public string SelfManagementInstructions { get; }

    public string ToolInstructions { get; }

    public IReadOnlyList<AgentToolDefinition> ToolDefinitions { get; }

    public string ConversationSummary { get; }

    public DateTimeOffset? ConversationSummaryUpdatedAt { get; }

    public string ConversationSummaryInstructions { get; }

    public bool HasMemories => Memories.Count > 0;

    public bool HasConversationSummary => !string.IsNullOrWhiteSpace(ConversationSummary);

    public bool HasConversationSummaryInstructions => !string.IsNullOrWhiteSpace(ConversationSummaryInstructions);

    public bool HasSelfManagementInstructions => !string.IsNullOrWhiteSpace(SelfManagementInstructions);

    public bool HasToolInstructions => !string.IsNullOrWhiteSpace(ToolInstructions);

    public bool HasToolDefinitions => ToolDefinitions.Count > 0;

    public bool HasSystemContext =>
        HasSelfManagementInstructions ||
        HasToolInstructions ||
        HasMemories ||
        HasConversationSummary ||
        HasConversationSummaryInstructions;
}
