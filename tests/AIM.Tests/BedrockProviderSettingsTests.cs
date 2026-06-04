using AIM.Providers.Bedrock;
using Microsoft.Extensions.Configuration;

namespace AIM.Tests;

public sealed class BedrockProviderSettingsTests
{
    [Fact]
    public void DefaultsToUsEastOneAndRequiresModel()
    {
        var settings = BedrockProviderSettings.FromConfiguration(new ConfigurationBuilder().Build());

        Assert.Equal("us-east-1", settings.Region);
        Assert.False(settings.IsConfigured);
    }

    [Fact]
    public void ReadsRegionAndModelFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AIM:Providers:Bedrock:Region"] = "us-west-2",
                ["AIM:Providers:Bedrock:ModelId"] = "amazon.nova-pro-v1:0"
            })
            .Build();

        var settings = BedrockProviderSettings.FromConfiguration(configuration);

        Assert.Equal("us-west-2", settings.Region);
        Assert.Equal("amazon.nova-pro-v1:0", settings.ModelId);
        Assert.True(settings.IsConfigured);
    }
}
