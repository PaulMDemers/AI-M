using AIM.Core.Memory;
using AIM.Core.Services;
using AIM.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIM.Storage.Services;

public sealed class SqliteMemorySuggestionService : IMemorySuggestionService
{
    private static readonly string[] MemorySignals =
    [
        "remember",
        "i prefer",
        "i like",
        "i am ",
        "i'm ",
        "my ",
        "call me",
        "working on",
        "we are building",
        "we're building"
    ];

    private readonly IDbContextFactory<AimDbContext> _dbContextFactory;

    public SqliteMemorySuggestionService(IDbContextFactory<AimDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task SuggestFromTurnAsync(
        Guid personalityId,
        Guid conversationId,
        string userMessage,
        string assistantMessage,
        CancellationToken cancellationToken = default)
    {
        var userText = Normalize(userMessage);
        var assistantText = Normalize(assistantMessage);
        var explicitMemory = BuildExplicitMemory(userText);

        if (explicitMemory is not null)
        {
            await RememberAsync(personalityId, explicitMemory, cancellationToken);
            return;
        }

        var suggestionText = BuildSuggestion(userText, assistantText);

        if (suggestionText is null)
        {
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var alreadyExists = await dbContext.MemorySuggestions
            .AnyAsync(
                suggestion =>
                    suggestion.PersonalityId == personalityId &&
                    suggestion.Content == suggestionText &&
                    suggestion.Status == "pending",
                cancellationToken);

        if (alreadyExists)
        {
            return;
        }

        dbContext.MemorySuggestions.Add(new MemorySuggestionEntity
        {
            Id = Guid.NewGuid(),
            PersonalityId = personalityId,
            ConversationId = conversationId,
            Content = suggestionText,
            Status = "pending",
            CreatedAt = DateTimeOffset.Now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemorySuggestion>> ListPendingAsync(
        Guid personalityId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var suggestions = await dbContext.MemorySuggestions
            .AsNoTracking()
            .Where(suggestion => suggestion.PersonalityId == personalityId && suggestion.Status == "pending")
            .ToListAsync(cancellationToken);

        return suggestions
            .OrderByDescending(suggestion => suggestion.CreatedAt)
            .Select(Map)
            .ToArray();
    }

    public async Task ApproveAsync(Guid suggestionId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var suggestion = await dbContext.MemorySuggestions
            .FirstOrDefaultAsync(item => item.Id == suggestionId, cancellationToken);

        if (suggestion is null || suggestion.Status != "pending")
        {
            return;
        }

        var memorySetId = await dbContext.MemorySets
            .AsNoTracking()
            .Where(memorySet => memorySet.PersonalityId == suggestion.PersonalityId)
            .Select(memorySet => memorySet.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (memorySetId == Guid.Empty)
        {
            memorySetId = Guid.NewGuid();
            dbContext.MemorySets.Add(new MemorySetEntity
            {
                Id = memorySetId,
                PersonalityId = suggestion.PersonalityId,
                Name = "Default memory",
                VectorCollectionName = $"personality_{suggestion.PersonalityId:N}"
            });
        }

        dbContext.MemoryRecords.Add(new MemoryRecordEntity
        {
            Id = Guid.NewGuid(),
            PersonalityId = suggestion.PersonalityId,
            MemorySetId = memorySetId,
            Content = suggestion.Content,
            Source = "suggested",
            CreatedAt = DateTimeOffset.Now
        });

        suggestion.Status = "approved";
        suggestion.ReviewedAt = DateTimeOffset.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(Guid suggestionId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var suggestion = await dbContext.MemorySuggestions
            .FirstOrDefaultAsync(item => item.Id == suggestionId, cancellationToken);

        if (suggestion is null || suggestion.Status != "pending")
        {
            return;
        }

        suggestion.Status = "rejected";
        suggestion.ReviewedAt = DateTimeOffset.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RememberAsync(Guid personalityId, string content, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var alreadyExists = await dbContext.MemoryRecords
            .AnyAsync(
                memory => memory.PersonalityId == personalityId && memory.Content == content,
                cancellationToken);

        if (alreadyExists)
        {
            return;
        }

        var memorySetId = await dbContext.MemorySets
            .AsNoTracking()
            .Where(memorySet => memorySet.PersonalityId == personalityId)
            .Select(memorySet => memorySet.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (memorySetId == Guid.Empty)
        {
            memorySetId = Guid.NewGuid();
            dbContext.MemorySets.Add(new MemorySetEntity
            {
                Id = memorySetId,
                PersonalityId = personalityId,
                Name = "Default memory",
                VectorCollectionName = $"personality_{personalityId:N}"
            });
        }

        dbContext.MemoryRecords.Add(new MemoryRecordEntity
        {
            Id = Guid.NewGuid(),
            PersonalityId = personalityId,
            MemorySetId = memorySetId,
            Content = content,
            Source = "explicit",
            CreatedAt = DateTimeOffset.Now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? BuildSuggestion(string userText, string assistantText)
    {
        if (userText.Length < 12 ||
            assistantText.StartsWith("Provider '", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!MemorySignals.Any(signal => userText.Contains(signal, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return userText.Length <= 220 ? userText : $"{userText[..217]}...";
    }

    private static string? BuildExplicitMemory(string userText)
    {
        var prefixes = new[]
        {
            "please remember that ",
            "remember that ",
            "please remember ",
            "remember "
        };

        foreach (var prefix in prefixes)
        {
            if (userText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var memory = userText[prefix.Length..].Trim();
                return memory.Length < 4 ? null : memory;
            }
        }

        return null;
    }

    private static string Normalize(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private static MemorySuggestion Map(MemorySuggestionEntity entity)
    {
        return new MemorySuggestion(
            entity.Id,
            entity.PersonalityId,
            entity.ConversationId,
            entity.Content,
            entity.Status,
            entity.CreatedAt);
    }
}
