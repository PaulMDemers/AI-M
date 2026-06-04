using AIM.Core.Personalities;
using AIM.Core.Providers;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class FriendViewModel
{
    public FriendViewModel(
        Personality personality,
        ProviderHealth health,
        ProviderDiagnosticResult? diagnostic = null)
    {
        Personality = personality;
        Health = health;
        Diagnostic = diagnostic;
    }

    public Personality Personality { get; }

    public ProviderHealth Health { get; }

    public ProviderDiagnosticResult? Diagnostic { get; }

    public string DisplayName => Personality.DisplayName;

    public string Status => Personality.Status;

    public string PresenceLabel => Diagnostic?.Label ?? Health.Label;

    public string PresenceDetail => Diagnostic?.Detail ?? Health.Detail;

    public string PresenceBrush => Diagnostic is not null ? Diagnostic.State switch
    {
        ProviderDiagnosticState.Ready => "#16A34A",
        ProviderDiagnosticState.Configured => "#1C7C7D",
        ProviderDiagnosticState.SetupNeeded => "#D97706",
        ProviderDiagnosticState.Disabled => "#6B7280",
        ProviderDiagnosticState.MissingProvider => "#DC2626",
        ProviderDiagnosticState.Unreachable => "#DC2626",
        ProviderDiagnosticState.Unauthorized => "#DC2626",
        ProviderDiagnosticState.Error => "#DC2626",
        _ => "#6B7280"
    } : Health.State switch
    {
        ProviderHealthState.Ready => "#16A34A",
        ProviderHealthState.NeedsSetup => "#D97706",
        ProviderHealthState.Disabled => "#6B7280",
        ProviderHealthState.MissingProvider => "#DC2626",
        _ => "#6B7280"
    };

    public string AvatarText => Personality.AvatarText;

    public string? AvatarImageUri => AvatarAssetResolver.Resolve(Personality.AvatarImagePath);

    public string Category => Personality.Category;

    public int CategorySortOrder => Category switch
    {
        "Core" => 0,
        "Archetypes" => 1,
        "Demo Figures" => 2,
        "Providers" => 3,
        "My Contacts" => 4,
        _ => 5
    };
}
