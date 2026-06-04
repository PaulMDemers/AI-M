using AIM.Core.Chat;
using AIM.Core.Personalities;
using AIM.Core.Services;
using AIM.Core.Tools;

namespace AIM.Providers.InMemory;

public sealed class InMemoryChatContextBuilder : IChatContextBuilder
{
    private readonly IMemoryService _memoryService;
    private readonly IConversationService _conversationService;
    private readonly IAgentToolRegistry _toolRegistry;

    public InMemoryChatContextBuilder(
        IMemoryService memoryService,
        IConversationService conversationService,
        IAgentToolRegistry toolRegistry)
    {
        _memoryService = memoryService;
        _conversationService = conversationService;
        _toolRegistry = toolRegistry;
    }

    public async Task<ChatContext> BuildAsync(
        Personality personality,
        Conversation? conversation = null,
        CancellationToken cancellationToken = default)
    {
        var memories = await _memoryService.GetMemoriesAsync(personality.Id, cancellationToken);
        var tools = _toolRegistry.ListTools();
        var latestConversation = conversation is null
            ? null
            : await _conversationService.GetConversationAsync(conversation.Id, cancellationToken);
        var summaryConversation = latestConversation ?? conversation;
        var visibleMessagesSinceSummary = summaryConversation is null
            ? 0
            : await CountVisibleMessagesSinceSummaryAsync(summaryConversation, cancellationToken);

        return new ChatContext(
            memories,
            ChatSelfManagementInstructions.Text,
            AgentToolInstructions.Build(tools),
            tools,
            summaryConversation?.Summary ?? string.Empty,
            summaryConversation?.SummaryUpdatedAt,
            ConversationSummaryInstructions.Build(summaryConversation, visibleMessagesSinceSummary));
    }

    private async Task<int> CountVisibleMessagesSinceSummaryAsync(
        Conversation conversation,
        CancellationToken cancellationToken)
    {
        var messages = await _conversationService.GetMessagesAsync(conversation.Id, cancellationToken);

        return messages.Count(message =>
            message.Role is ChatRole.User or ChatRole.Assistant &&
            (conversation.SummaryUpdatedAt is null || message.CreatedAt > conversation.SummaryUpdatedAt));
    }
}
