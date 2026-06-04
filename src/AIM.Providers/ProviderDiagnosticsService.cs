using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AIM.Core.Providers;
using AIM.Core.Services;

namespace AIM.Providers;

public sealed class ProviderDiagnosticsService : IProviderDiagnosticsService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public ProviderDiagnosticsService()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(8) }, ownsHttpClient: true)
    {
    }

    public ProviderDiagnosticsService(HttpClient httpClient)
        : this(httpClient, ownsHttpClient: false)
    {
    }

    private ProviderDiagnosticsService(HttpClient httpClient, bool ownsHttpClient)
    {
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
    }

    public async Task<ProviderDiagnosticResult> CheckAsync(
        ProviderAccount account,
        bool providerRegistered,
        CancellationToken cancellationToken = default)
    {
        var health = ProviderHealthEvaluator.EvaluateAccount(account, providerRegistered);

        if (!providerRegistered)
        {
            return Result(
                account,
                ProviderDiagnosticState.MissingProvider,
                "Offline",
                health.Detail,
                isConfigured: false,
                isVerified: false);
        }

        if (!account.IsEnabled)
        {
            return Result(
                account,
                ProviderDiagnosticState.Disabled,
                "Disabled",
                health.Detail,
                isConfigured: false,
                isVerified: false);
        }

        if (!health.IsReady)
        {
            return Result(
                account,
                ProviderDiagnosticState.SetupNeeded,
                health.Label,
                health.Detail,
                isConfigured: false,
                isVerified: false);
        }

        return account.ProviderKind.ToLowerInvariant() switch
        {
            "fake" => Result(account, ProviderDiagnosticState.Ready, "Verified", $"{account.DisplayName} is ready.", true, true),
            "openai" => await CheckOpenAiAsync(account, cancellationToken),
            "ollama" => await CheckOllamaAsync(account, cancellationToken),
            "bedrock" => CheckBedrock(account),
            _ => Result(account, ProviderDiagnosticState.Configured, "Configured", $"{account.DisplayName} is configured.", true, false)
        };
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<ProviderDiagnosticResult> CheckOpenAiAsync(
        ProviderAccount account,
        CancellationToken cancellationToken)
    {
        var modelId = account.DefaultModelId?.Trim();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.openai.com/v1/models/{Uri.EscapeDataString(modelId ?? string.Empty)}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            account.Credential?.Trim());

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return Result(
                    account,
                    ProviderDiagnosticState.Ready,
                    "Verified",
                    $"OpenAI accepted the API key and model '{modelId}'.",
                    isConfigured: true,
                    isVerified: true);
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return Result(
                    account,
                    ProviderDiagnosticState.Unauthorized,
                    "Auth failed",
                    "OpenAI rejected the API key.",
                    isConfigured: true,
                    isVerified: false);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result(
                    account,
                    ProviderDiagnosticState.SetupNeeded,
                    "Model not found",
                    $"OpenAI did not find model '{modelId}'.",
                    isConfigured: false,
                    isVerified: true);
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            return Result(
                account,
                ProviderDiagnosticState.Error,
                "Check failed",
                $"OpenAI returned {(int)response.StatusCode} {response.ReasonPhrase}: {Trim(error)}",
                isConfigured: true,
                isVerified: false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return Result(
                account,
                ProviderDiagnosticState.Unreachable,
                "Unreachable",
                $"OpenAI could not be reached. {ex.Message}",
                isConfigured: true,
                isVerified: false);
        }
    }

    private async Task<ProviderDiagnosticResult> CheckOllamaAsync(
        ProviderAccount account,
        CancellationToken cancellationToken)
    {
        var endpoint = account.Endpoint?.Trim();
        var modelId = account.DefaultModelId?.Trim();

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri))
        {
            return Result(
                account,
                ProviderDiagnosticState.SetupNeeded,
                "Setup needed",
                "Ollama endpoint must be an absolute HTTP URL.",
                isConfigured: false,
                isVerified: false);
        }

        try
        {
            var tagsUri = new Uri(baseUri, "/api/tags");
            var tags = await _httpClient.GetFromJsonAsync<OllamaTagsResponse>(tagsUri, cancellationToken);
            var names = tags?.Models
                .Select(model => model.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray() ?? [];

            if (names.Any(name => ModelMatches(name, modelId)))
            {
                return Result(
                    account,
                    ProviderDiagnosticState.Ready,
                    "Verified",
                    $"Ollama is reachable and model '{modelId}' is installed.",
                    isConfigured: true,
                    isVerified: true);
            }

            var installed = names.Length == 0 ? "No local models were reported." : $"Installed: {string.Join(", ", names.Take(6))}.";
            return Result(
                account,
                ProviderDiagnosticState.SetupNeeded,
                "Model missing",
                $"Ollama is reachable, but model '{modelId}' was not found. {installed}",
                isConfigured: false,
                isVerified: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return Result(
                account,
                ProviderDiagnosticState.Unreachable,
                "Unreachable",
                $"Ollama could not be reached at {endpoint}. {ex.Message}",
                isConfigured: true,
                isVerified: false);
        }
    }

    private static ProviderDiagnosticResult CheckBedrock(ProviderAccount account)
    {
        return Result(
            account,
            ProviderDiagnosticState.Configured,
            "Configured",
            "AWS Bedrock has a region and model configured. Runtime credentials will be verified on first request.",
            isConfigured: true,
            isVerified: false);
    }

    private static ProviderDiagnosticResult Result(
        ProviderAccount account,
        ProviderDiagnosticState state,
        string label,
        string detail,
        bool isConfigured,
        bool isVerified)
    {
        return new ProviderDiagnosticResult(
            account.Key,
            state,
            label,
            detail,
            isConfigured,
            isVerified);
    }

    private static bool ModelMatches(string installedModel, string? requestedModel)
    {
        if (string.IsNullOrWhiteSpace(requestedModel))
        {
            return false;
        }

        return string.Equals(installedModel, requestedModel, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(installedModel, $"{requestedModel}:latest", StringComparison.OrdinalIgnoreCase);
    }

    private static string Trim(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= 240 ? trimmed : $"{trimmed[..240]}...";
    }

    private sealed record OllamaTagsResponse(
        [property: JsonPropertyName("models")] IReadOnlyList<OllamaModel> Models);

    private sealed record OllamaModel(
        [property: JsonPropertyName("name")] string Name);
}
