using AIM.Core.Chat;

namespace AIM.Core.Services;

public interface IConversationService
{
    Task<ConversationGroup> GetOrCreateConversationGroupAsync(
        Guid personalityId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationGroup>> ListConversationGroupsAsync(
        Guid personalityId,
        CancellationToken cancellationToken = default);

    Task<ConversationGroup> CreateConversationGroupAsync(
        Guid personalityId,
        string title,
        CancellationToken cancellationToken = default);

    Task RenameConversationGroupAsync(
        Guid groupId,
        string title,
        CancellationToken cancellationToken = default);

    Task ArchiveConversationGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<Conversation> GetOrCreateConversationAsync(
        Guid personalityId,
        Guid? groupId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Conversation>> ListConversationsAsync(
        Guid personalityId,
        Guid? groupId = null,
        CancellationToken cancellationToken = default);

    Task<Conversation> CreateConversationAsync(
        Guid personalityId,
        string title,
        Guid? groupId = null,
        CancellationToken cancellationToken = default);

    Task RenameConversationAsync(
        Guid conversationId,
        string title,
        CancellationToken cancellationToken = default);

    Task ArchiveConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<Conversation?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task UpdateConversationSummaryAsync(
        Guid conversationId,
        string summary,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<ChatMessage> AddMessageAsync(
        Guid conversationId,
        ChatRole role,
        string content,
        CancellationToken cancellationToken = default);
}
