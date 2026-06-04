using AIM.Core.Providers;
using AIM.Providers;
using AIM.Providers.Bedrock;
using AIM.Providers.Fakes;
using AIM.Providers.Ollama;
using AIM.Providers.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Tests;

public sealed class ProviderRegistrationTests
{
    [Fact]
    public void RegistersFakeProviderWhenOpenAiIsNotConfigured()
    {
        var services = new ServiceCollection();

        services.AddAimPreviewProviders(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var aiProviders = provider.GetServices<IAiProvider>().ToArray();

        Assert.Single(aiProviders);
        Assert.IsType<FakeAiProvider>(aiProviders[0]);
    }

    [Fact]
    public void RegistersOpenAiProviderWhenApiKeyIsConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AIM:Providers:OpenAI:ApiKey"] = "test-key",
                ["AIM:Providers:OpenAI:ModelId"] = "test-model"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddAimPreviewProviders(configuration);

        using var provider = services.BuildServiceProvider();
        var aiProviders = provider.GetServices<IAiProvider>().ToArray();

        Assert.Contains(aiProviders, aiProvider => aiProvider is FakeAiProvider);
        Assert.Contains(aiProviders, aiProvider => aiProvider is OpenAiProvider openAi && openAi.ModelId == "test-model");
        Assert.Contains(aiProviders, aiProvider => aiProvider is OpenAiProvider { SupportsNativeTools: true });
    }

    [Fact]
    public void RegistersOllamaProviderWhenModelIsConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AIM:Providers:Ollama:Endpoint"] = "http://localhost:11434",
                ["AIM:Providers:Ollama:ModelId"] = "gemma3"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddAimPreviewProviders(configuration);

        using var provider = services.BuildServiceProvider();
        var aiProviders = provider.GetServices<IAiProvider>().ToArray();

        Assert.Contains(aiProviders, aiProvider => aiProvider is FakeAiProvider);
        Assert.Contains(aiProviders, aiProvider => aiProvider is OllamaProvider ollama && ollama.Settings.ModelId == "gemma3");
    }

    [Fact]
    public void RegistersBedrockProviderWhenModelIsConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AIM:Providers:Bedrock:Region"] = "us-west-2",
                ["AIM:Providers:Bedrock:ModelId"] = "anthropic.claude-3-5-sonnet-20241022-v2:0"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddAimPreviewProviders(configuration);

        using var provider = services.BuildServiceProvider();
        var aiProviders = provider.GetServices<IAiProvider>().ToArray();

        Assert.Contains(aiProviders, aiProvider => aiProvider is FakeAiProvider);
        Assert.Contains(aiProviders, aiProvider => aiProvider is BedrockProvider);
    }
}
