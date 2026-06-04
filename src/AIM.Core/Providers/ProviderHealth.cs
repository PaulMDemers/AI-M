using AIM.Core.Personalities;

namespace AIM.Core.Providers;

public enum ProviderHealthState
{
    Ready,
    NeedsSetup,
    Disabled,
    MissingProvider
}

public sealed record ProviderHealth(
    ProviderHealthState State,
    string Label,
    string Detail)
{
    public bool IsReady => State == ProviderHealthState.Ready;
}

public static class ProviderHealthEvaluator
{
    public static ProviderHealth EvaluateAccount(
        ProviderAccount account,
        bool providerRegistered)
    {
        if (!providerRegistered)
        {
            return new ProviderHealth(
                ProviderHealthState.MissingProvider,
                "Offline",
                $"Provider '{account.Key}' is not available in this app.");
        }

        if (!account.IsEnabled)
        {
            return new ProviderHealth(
                ProviderHealthState.Disabled,
                "Disabled",
                $"{account.DisplayName} is disabled in Provider Settings.");
        }

        return account.ProviderKind.ToLowerInvariant() switch
        {
            "fake" => Ready(account),
            "openai" => EvaluateOpenAi(account),
            "ollama" => EvaluateOllama(account),
            "bedrock" => EvaluateBedrock(account),
            _ => Ready(account)
        };
    }

    public static ProviderHealth Evaluate(
        Personality personality,
        ProviderAccount? account,
        bool providerRegistered)
    {
        if (!providerRegistered)
        {
            return new ProviderHealth(
                ProviderHealthState.MissingProvider,
                "Offline",
                $"Provider '{personality.ProviderKey}' is not available in this app.");
        }

        if (account is null)
        {
            return new ProviderHealth(
                ProviderHealthState.NeedsSetup,
                "Setup needed",
                $"Provider account '{personality.ProviderKey}' is missing.");
        }

        return EvaluateAccount(account, providerRegistered);
    }

    private static ProviderHealth EvaluateOpenAi(ProviderAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.Credential))
        {
            return new ProviderHealth(
                ProviderHealthState.NeedsSetup,
                "Setup needed",
                $"{account.DisplayName} needs an API key.");
        }

        if (string.IsNullOrWhiteSpace(account.DefaultModelId))
        {
            return new ProviderHealth(
                ProviderHealthState.NeedsSetup,
                "Setup needed",
                $"{account.DisplayName} needs a model.");
        }

        return Ready(account);
    }

    private static ProviderHealth EvaluateOllama(ProviderAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.DefaultModelId))
        {
            return new ProviderHealth(
                ProviderHealthState.NeedsSetup,
                "Setup needed",
                $"{account.DisplayName} needs a local model.");
        }

        return Ready(account);
    }

    private static ProviderHealth EvaluateBedrock(ProviderAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.Endpoint))
        {
            return new ProviderHealth(
                ProviderHealthState.NeedsSetup,
                "Setup needed",
                $"{account.DisplayName} needs a region.");
        }

        if (string.IsNullOrWhiteSpace(account.DefaultModelId))
        {
            return new ProviderHealth(
                ProviderHealthState.NeedsSetup,
                "Setup needed",
                $"{account.DisplayName} needs a model.");
        }

        return Ready(account);
    }

    private static ProviderHealth Ready(ProviderAccount account)
    {
        return new ProviderHealth(
            ProviderHealthState.Ready,
            "Online",
            $"{account.DisplayName} is ready.");
    }
}
