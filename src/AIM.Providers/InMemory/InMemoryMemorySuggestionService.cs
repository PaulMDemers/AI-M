using AIM.Core.Memory;
using AIM.Core.Services;

namespace AIM.Providers.InMemory;

public sealed class InMemoryMemorySuggestionService : IMemorySuggestionService
{
    private readonly Lock _gate = new();
    private readonly List<MemorySuggestion> _suggestions = [];

    public Task SuggestFromTurnAsync(
        Guid personalityId,
        Guid conversationId,
        string userMessage,
        string assistantMessage,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (userMessage.Contains("remember", StringComparison.OrdinalIgnoreCase))
            {
                _suggestions.Add(new MemorySuggestion(
                    Guid.NewGuid(),
                    personalityId,
                    conversationId,
                    userMessage.Trim(),
                    "pending",
                    DateTimeOffset.Now));
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MemorySuggestion>> ListPendingAsync(
        Guid personalityId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<MemorySuggestion>>(
                _suggestions
                    .Where(suggestion => suggestion.PersonalityId == personalityId && suggestion.Status == "pending")
                    .ToArray());
        }
    }

    public Task ApproveAsync(Guid suggestionId, CancellationToken cancellationToken = default)
    {
        Remove(suggestionId);
        return Task.CompletedTask;
    }

    public Task RejectAsync(Guid suggestionId, CancellationToken cancellationToken = default)
    {
        Remove(suggestionId);
        return Task.CompletedTask;
    }

    private void Remove(Guid suggestionId)
    {
        lock (_gate)
        {
            _suggestions.RemoveAll(suggestion => suggestion.Id == suggestionId);
        }
    }
}
