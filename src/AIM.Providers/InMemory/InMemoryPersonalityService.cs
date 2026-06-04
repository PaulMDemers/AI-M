using AIM.Core.Personalities;
using AIM.Core.Services;

namespace AIM.Providers.InMemory;

public sealed class InMemoryPersonalityService : IPersonalityService
{
    private readonly Lock _gate = new();
    private readonly List<Personality> _personalities =
    [
        new(
            Guid.Parse("3d4a7d20-131e-4f8b-b43d-7630f2658e39"),
            "Ada",
            "Architecture mode",
            "A",
            "You are Ada, a careful software architecture partner. Be concise, concrete, and implementation-minded.",
            Guid.Parse("bb53644d-6655-4bff-9ba0-f91d6db93e28"),
            "fake",
            "fake-preview"),
        new(
            Guid.Parse("9683c667-5c85-4e3f-8e1c-f528b61de313"),
            "Minsky",
            "Memory scout",
            "M",
            "You are Minsky, an assistant focused on memory, retrieval, and long-running project context.",
            Guid.Parse("c82830f7-74ce-4710-bc42-231a4e038e13"),
            "fake",
            "fake-preview"),
        new(
            Guid.Parse("fa8924d4-6a49-4d8a-b1ef-1fd9a8f0f4d9"),
            "Tess",
            "UI polish",
            "T",
            "You are Tess, a product-minded UI collaborator. Favor direct, useful interface choices.",
            Guid.Parse("e4a2f7a9-2195-43bd-92e9-b5a818a77b7c"),
            "fake",
            "fake-preview"),
        new(
            Guid.Parse("85f8e663-8fb9-46f7-a2f1-c51c11072b50"),
            "Nova",
            "OpenAI",
            "N",
            "You are Nova, a practical AI assistant for focused desktop conversations.",
            Guid.Parse("9b7a65f7-a15e-4784-81fc-2f3f3ec23adc"),
            "openai",
            "gpt-4.1-mini"),
        new(
            Guid.Parse("ac490f46-7c3c-4b1d-849e-a86034ebd9bf"),
            "Local",
            "Ollama",
            "L",
            "You are Local, a private local-model assistant. Be direct and useful.",
            Guid.Parse("dfc84d23-d45a-46a3-9e1f-3e792db0f587"),
            "ollama",
            "local-configured-model")
    ];

    public Task<IReadOnlyList<Personality>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Personality>>(_personalities.ToArray());
        }
    }

    public Task<Personality?> GetAsync(Guid personalityId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_personalities.FirstOrDefault(personality => personality.Id == personalityId));
        }
    }

    public Task<Personality> SaveAsync(PersonalityDraft draft, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var existingIndex = _personalities.FindIndex(personality => personality.Id == draft.Id);
            var existing = existingIndex >= 0 ? _personalities[existingIndex] : null;
            var personality = new Personality(
                draft.Id ?? Guid.NewGuid(),
                draft.DisplayName.Trim(),
                draft.Status.Trim(),
                draft.AvatarText.Trim(),
                draft.SystemPrompt.Trim(),
                existing?.MemorySetId ?? Guid.NewGuid(),
                draft.ProviderKey.Trim(),
                draft.ModelId.Trim(),
                draft.AvatarImagePath.Trim(),
                string.IsNullOrWhiteSpace(draft.Category) ? "My Contacts" : draft.Category.Trim());

            if (existingIndex >= 0)
            {
                _personalities[existingIndex] = personality;
            }
            else
            {
                _personalities.Add(personality);
            }

            return Task.FromResult(personality);
        }
    }

    public Task DeleteAsync(Guid personalityId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _personalities.RemoveAll(personality => personality.Id == personalityId);
            return Task.CompletedTask;
        }
    }
}
