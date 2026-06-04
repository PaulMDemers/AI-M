namespace AIM.Core.Providers;

public enum ProviderDiagnosticState
{
    Ready,
    Configured,
    SetupNeeded,
    Disabled,
    MissingProvider,
    Unreachable,
    Unauthorized,
    Error
}

public sealed record ProviderDiagnosticResult(
    string ProviderKey,
    ProviderDiagnosticState State,
    string Label,
    string Detail,
    bool IsConfigured,
    bool IsVerified)
{
    public bool IsUsable => State is ProviderDiagnosticState.Ready or ProviderDiagnosticState.Configured;
}
