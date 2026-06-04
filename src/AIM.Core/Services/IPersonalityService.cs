using AIM.Core.Personalities;

namespace AIM.Core.Services;

public interface IPersonalityService
{
    Task<IReadOnlyList<Personality>> ListAsync(CancellationToken cancellationToken = default);

    Task<Personality?> GetAsync(Guid personalityId, CancellationToken cancellationToken = default);

    Task<Personality> SaveAsync(PersonalityDraft draft, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid personalityId, CancellationToken cancellationToken = default);
}
