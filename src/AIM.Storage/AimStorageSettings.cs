using Microsoft.Extensions.Configuration;

namespace AIM.Storage;

public sealed class AimStorageSettings
{
    public const string SectionName = "AIM:Storage";

    public required string DatabasePath { get; init; }

    public string ConnectionString => $"Data Source={DatabasePath};Pooling=False";

    public static AimStorageSettings FromConfiguration(IConfiguration? configuration)
    {
        var configuredPath =
            configuration?.GetSection(SectionName)["SqlitePath"] ??
            configuration?["AIM_SQLITE_PATH"] ??
            Environment.GetEnvironmentVariable("AIM_SQLITE_PATH");

        var databasePath = string.IsNullOrWhiteSpace(configuredPath)
            ? GetDefaultDatabasePath()
            : Environment.ExpandEnvironmentVariables(configuredPath.Trim());

        var directory = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new AimStorageSettings
        {
            DatabasePath = databasePath
        };
    }

    private static string GetDefaultDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "AI-M", "aim.db");
    }
}
