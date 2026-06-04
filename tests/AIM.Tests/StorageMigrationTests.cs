using AIM.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Tests;

public sealed class StorageMigrationTests
{
    [Fact]
    public async Task InitializerAppliesEfMigrations()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-migration-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildProvider(databasePath);
            await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

            await using var dbContext = await provider
                .GetRequiredService<IDbContextFactory<AimDbContext>>()
                .CreateDbContextAsync();
            var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();

            Assert.Contains(appliedMigrations, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
            Assert.Contains(appliedMigrations, migration => migration.EndsWith("_PersonalityCategory", StringComparison.Ordinal));
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
