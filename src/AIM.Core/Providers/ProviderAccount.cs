namespace AIM.Core.Providers;

public sealed class ProviderAccount
{
    public ProviderAccount(
        Guid id,
        string key,
        string displayName,
        string providerKind,
        string? endpoint,
        string? defaultModelId,
        string? credential,
        bool isEnabled)
    {
        Id = id;
        Key = key;
        DisplayName = displayName;
        ProviderKind = providerKind;
        Endpoint = endpoint;
        DefaultModelId = defaultModelId;
        Credential = credential;
        IsEnabled = isEnabled;
    }

    public Guid Id { get; }

    public string Key { get; }

    public string DisplayName { get; }

    public string ProviderKind { get; }

    public string? Endpoint { get; }

    public string? DefaultModelId { get; }

    public string? Credential { get; }

    public bool IsEnabled { get; }
}
