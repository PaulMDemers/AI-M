using AIM.Core.Services;
using AIM.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Tests;

public sealed class ProviderAccountStorageTests
{
    [Fact]
    public async Task ProviderAccountServicePersistsProviderSettings()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-provider-tests-{Guid.NewGuid():N}.db");

        try
        {
            await using (var provider = BuildProvider(databasePath))
            {
                await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();
                var accounts = provider.GetRequiredService<IProviderAccountService>();

                await accounts.SaveAsync(
                    "openai",
                    "OpenAI",
                    "openai",
                    endpoint: null,
                    defaultModelId: "gpt-test",
                    credential: "secret-key",
                    isEnabled: true);
            }

            await using (var provider = BuildProvider(databasePath))
            {
                await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

                var account = await provider.GetRequiredService<IProviderAccountService>().GetAsync("openai");

                Assert.NotNull(account);
                Assert.Equal("gpt-test", account.DefaultModelId);
                Assert.Equal("secret-key", account.Credential);

                var bedrock = await provider.GetRequiredService<IProviderAccountService>().GetAsync("bedrock");

                Assert.NotNull(bedrock);
                Assert.Equal("us-east-1", bedrock.Endpoint);
            }
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static ServiceProvider BuildProvider(string databasePath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AIM:Storage:SqlitePath"] = databasePath
            })
            .Build();
        var services = new ServiceCollection();

        services.AddAimStorage(configuration);

        return services.BuildServiceProvider();
    }
}
