namespace AIM.Storage.Entities;

public sealed class ProviderAccountEntity
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ProviderKind { get; set; } = string.Empty;

    public string? Endpoint { get; set; }

    public string? DefaultModelId { get; set; }

    public byte[]? ProtectedCredential { get; set; }

    public bool IsEnabled { get; set; } = true;
}
