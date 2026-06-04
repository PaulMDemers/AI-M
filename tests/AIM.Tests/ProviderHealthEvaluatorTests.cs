using AIM.Core.Personalities;
using AIM.Core.Providers;

namespace AIM.Tests;

public sealed class ProviderHealthEvaluatorTests
{
    [Fact]
    public void OpenAiRequiresCredentialAndModel()
    {
        var personality = Personality("openai");
        var missingKey = Account("openai", "openai", credential: null, modelId: "gpt-4.1-mini");
        var ready = Account("openai", "openai", credential: "key", modelId: "gpt-4.1-mini");

        Assert.Equal(ProviderHealthState.NeedsSetup, ProviderHealthEvaluator.Evaluate(personality, missingKey, true).State);
        Assert.Equal(ProviderHealthState.Ready, ProviderHealthEvaluator.Evaluate(personality, ready, true).State);
    }

    [Fact]
    public void ProviderAccountsCanBeCheckedDirectly()
    {
        var missingKey = Account("openai", "openai", credential: null, modelId: "gpt-4.1-mini");
        var ready = Account("openai", "openai", credential: "key", modelId: "gpt-4.1-mini");

        Assert.Equal(ProviderHealthState.NeedsSetup, ProviderHealthEvaluator.EvaluateAccount(missingKey, true).State);
        Assert.Equal(ProviderHealthState.Ready, ProviderHealthEvaluator.EvaluateAccount(ready, true).State);
        Assert.Equal(ProviderHealthState.MissingProvider, ProviderHealthEvaluator.EvaluateAccount(ready, false).State);
    }

    [Fact]
    public void DisabledAndMissingProvidersAreReported()
    {
        var personality = Personality("openai");
        var disabled = Account("openai", "openai", credential: "key", modelId: "gpt-4.1-mini", isEnabled: false);

        Assert.Equal(ProviderHealthState.Disabled, ProviderHealthEvaluator.Evaluate(personality, disabled, true).State);
        Assert.Equal(ProviderHealthState.MissingProvider, ProviderHealthEvaluator.Evaluate(personality, disabled, false).State);
    }

    [Fact]
    public void OllamaAndBedrockRequireProviderSpecificSettings()
    {
        var ollama = Personality("ollama");
        var bedrock = Personality("bedrock");

        Assert.Equal(
            ProviderHealthState.NeedsSetup,
            ProviderHealthEvaluator.Evaluate(ollama, Account("ollama", "ollama", modelId: null), true).State);
        Assert.Equal(
            ProviderHealthState.Ready,
            ProviderHealthEvaluator.Evaluate(ollama, Account("ollama", "ollama", modelId: "llama3.2"), true).State);
        Assert.Equal(
            ProviderHealthState.NeedsSetup,
            ProviderHealthEvaluator.Evaluate(bedrock, Account("bedrock", "bedrock", endpoint: "us-east-1", modelId: null), true).State);
        Assert.Equal(
            ProviderHealthState.Ready,
            ProviderHealthEvaluator.Evaluate(bedrock, Account("bedrock", "bedrock", endpoint: "us-east-1", modelId: "model"), true).State);
    }

    private static Personality Personality(string providerKey)
    {
        return new Personality(
            Guid.NewGuid(),
            "Test",
            "Testing",
            "T",
            "You are Test.",
            Guid.NewGuid(),
            providerKey,
            "model");
    }

    private static ProviderAccount Account(
        string key,
        string kind,
        string? endpoint = null,
        string? credential = null,
        string? modelId = null,
        bool isEnabled = true)
    {
        return new ProviderAccount(
            Guid.NewGuid(),
            key,
            key,
            kind,
            endpoint,
            modelId,
            credential,
            isEnabled);
    }
}
