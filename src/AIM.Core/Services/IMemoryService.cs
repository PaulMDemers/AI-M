using AIM.Core.Memory;

namespace AIM.Core.Services;

public interface IMemoryService
{
    Task<IReadOnlyList<MemoryRecord>> GetMemoriesAsync(Guid personalityId, CancellationToken cancellationToken = default);

    Task<MemoryRecord> RememberAsync(Guid personalityId, string content, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid memoryId, CancellationToken cancellationToken = default);
}
