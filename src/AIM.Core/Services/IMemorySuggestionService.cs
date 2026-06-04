using AIM.Core.Memory;

namespace AIM.Core.Services;

public interface IMemorySuggestionService
{
    Task SuggestFromTurnAsync(
        Guid personalityId,
        Guid conversationId,
        string userMessage,
        string assistantMessage,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemorySuggestion>> ListPendingAsync(
        Guid personalityId,
        CancellationToken cancellationToken = default);

    Task ApproveAsync(Guid suggestionId, CancellationToken cancellationToken = default);

    Task RejectAsync(Guid suggestionId, CancellationToken cancellationToken = default);
}
