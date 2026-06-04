using AIM.Providers.Ollama;
using Microsoft.Extensions.Configuration;

namespace AIM.Tests;

public sealed class OllamaProviderSettingsTests
{
    [Fact]
    public void DefaultsToLocalEndpointAndRequiresModel()
    {
        var settings = OllamaProviderSettings.FromConfiguration(new ConfigurationBuilder().Build());

        Assert.Equal(new Uri("http://localhost:11434"), settings.Endpoint);
        Assert.False(settings.IsConfigured);
    }

    [Fact]
    public void ReadsEndpointAndModelFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AIM:Providers:Ollama:Endpoint"] = "http://127.0.0.1:11434",
                ["AIM:Providers:Ollama:ModelId"] = "qwen3"
            })
            .Build();

        var settings = OllamaProviderSettings.FromConfiguration(configuration);

        Assert.Equal(new Uri("http://127.0.0.1:11434"), settings.Endpoint);
        Assert.Equal("qwen3", settings.ModelId);
        Assert.True(settings.IsConfigured);
    }
}
