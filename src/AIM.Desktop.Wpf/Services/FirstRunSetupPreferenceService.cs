using System.IO;
using System.Text.Json;

namespace AIM.Desktop.Wpf.Services;

public sealed class FirstRunSetupPreferenceService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public FirstRunSetupPreferenceService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsPath = Path.Combine(appData, "AI-M", "setup-preferences.json");
    }

    public async Task<bool> GetUseDemoModeAsync(CancellationToken cancellationToken = default)
    {
        if (bool.TryParse(Environment.GetEnvironmentVariable("AIM_DEMO_MODE"), out var useDemoMode) && useDemoMode)
        {
            return true;
        }

        if (!File.Exists(_settingsPath))
        {
            return false;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<SetupPreferences>(stream, cancellationToken: cancellationToken);
            return settings?.UseDemoMode == true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public async Task SetUseDemoModeAsync(bool useDemoMode, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(
            stream,
            new SetupPreferences(useDemoMode),
            SerializerOptions,
            cancellationToken);
    }

    private sealed record SetupPreferences(bool UseDemoMode);
}
