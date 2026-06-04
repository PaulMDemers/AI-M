using AIM.Core.Memory;
using AIM.Core.Services;

namespace AIM.Providers.InMemory;

public sealed class InMemoryMemoryService : IMemoryService
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, List<MemoryRecord>> _memoriesByPersonality = [];

    public Task<IReadOnlyList<MemoryRecord>> GetMemoriesAsync(Guid personalityId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_memoriesByPersonality.TryGetValue(personalityId, out var memories))
            {
                return Task.FromResult<IReadOnlyList<MemoryRecord>>([]);
            }

            return Task.FromResult<IReadOnlyList<MemoryRecord>>(memories.ToArray());
        }
    }

    public Task<MemoryRecord> RememberAsync(Guid personalityId, string content, CancellationToken cancellationToken = default)
    {
        var memory = new MemoryRecord(Guid.NewGuid(), personalityId, content.Trim(), DateTimeOffset.Now);

        lock (_gate)
        {
            if (!_memoriesByPersonality.TryGetValue(personalityId, out var memories))
            {
                memories = [];
                _memoriesByPersonality[personalityId] = memories;
            }

            memories.Add(memory);
        }

        return Task.FromResult(memory);
    }

    public Task DeleteAsync(Guid memoryId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            foreach (var memories in _memoriesByPersonality.Values)
            {
                memories.RemoveAll(memory => memory.Id == memoryId);
            }

            return Task.CompletedTask;
        }
    }
}
