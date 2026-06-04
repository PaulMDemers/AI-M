using AIM.Core.Providers;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class ProviderOptionViewModel
{
    public ProviderOptionViewModel(ProviderAccount account)
    {
        Key = account.Key;
        DisplayName = account.DisplayName;
        DefaultModelId = account.DefaultModelId ?? string.Empty;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string DefaultModelId { get; }
}
