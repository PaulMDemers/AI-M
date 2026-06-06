using AIM.Core.Personalities;
using AIM.Core.Providers;

namespace AIM.Desktop.WinForms;

internal sealed class FriendListItem
{
    public FriendListItem(
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

    public string PresenceLabel => Diagnostic?.Label ?? Health.Label;

    public string PresenceDetail => Diagnostic?.Detail ?? Health.Detail;

    public bool IsReady => Diagnostic?.IsUsable ?? Health.IsReady;

    public string DisplayText => Personality.DisplayName;

    public override string ToString()
    {
        return DisplayText;
    }
}
