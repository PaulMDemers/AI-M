using AIM.Core.Providers;
using AIM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class ProviderSettingsViewModel : ObservableObject
{
    private readonly IProviderAccountService _providerAccountService;
    private readonly IProviderDiagnosticsService _providerDiagnosticsService;
    private readonly IProviderStatusService _providerStatusService;
    private readonly IReadOnlySet<string> _registeredProviderKeys;
    private string _openAiApiKey = string.Empty;
    private string _openAiModelId = "gpt-4.1-mini";
    private string _ollamaEndpoint = "http://localhost:11434";
    private string _ollamaModelId = string.Empty;
    private string _bedrockRegion = "us-east-1";
    private string _bedrockModelId = string.Empty;
    private int _selectedProviderIndex;
    private string _openAiStatus = string.Empty;
    private string _openAiStatusBrush = "#657085";
    private string _ollamaStatus = string.Empty;
    private string _ollamaStatusBrush = "#657085";
    private string _bedrockStatus = string.Empty;
    private string _bedrockStatusBrush = "#657085";
    private string _status = string.Empty;

    public ProviderSettingsViewModel(
        IProviderAccountService providerAccountService,
        IProviderDiagnosticsService providerDiagnosticsService,
        IProviderStatusService providerStatusService,
        IEnumerable<IAiProvider> providers)
    {
        _providerAccountService = providerAccountService;
        _providerDiagnosticsService = providerDiagnosticsService;
        _providerStatusService = providerStatusService;
        _registeredProviderKeys = providers
            .Select(provider => provider.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CheckOpenAiCommand = new AsyncRelayCommand(CheckOpenAiAsync);
        CheckOllamaCommand = new AsyncRelayCommand(CheckOllamaAsync);
        CheckBedrockCommand = new AsyncRelayCommand(CheckBedrockAsync);
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand CheckOpenAiCommand { get; }

    public IAsyncRelayCommand CheckOllamaCommand { get; }

    public IAsyncRelayCommand CheckBedrockCommand { get; }

    public int SelectedProviderIndex
    {
        get => _selectedProviderIndex;
        set => SetProperty(ref _selectedProviderIndex, value);
    }

    public string OpenAiApiKey
    {
        get => _openAiApiKey;
        set => SetProperty(ref _openAiApiKey, value);
    }

    public string OpenAiModelId
    {
        get => _openAiModelId;
        set => SetProperty(ref _openAiModelId, value);
    }

    public string OllamaEndpoint
    {
        get => _ollamaEndpoint;
        set => SetProperty(ref _ollamaEndpoint, value);
    }

    public string OllamaModelId
    {
        get => _ollamaModelId;
        set => SetProperty(ref _ollamaModelId, value);
    }

    public string BedrockRegion
    {
        get => _bedrockRegion;
        set => SetProperty(ref _bedrockRegion, value);
    }

    public string BedrockModelId
    {
        get => _bedrockModelId;
        set => SetProperty(ref _bedrockModelId, value);
    }

    public string OpenAiStatus
    {
        get => _openAiStatus;
        private set => SetProperty(ref _openAiStatus, value);
    }

    public string OpenAiStatusBrush
    {
        get => _openAiStatusBrush;
        private set => SetProperty(ref _openAiStatusBrush, value);
    }

    public string OllamaStatus
    {
        get => _ollamaStatus;
        private set => SetProperty(ref _ollamaStatus, value);
    }

    public string OllamaStatusBrush
    {
        get => _ollamaStatusBrush;
        private set => SetProperty(ref _ollamaStatusBrush, value);
    }

    public string BedrockStatus
    {
        get => _bedrockStatus;
        private set => SetProperty(ref _bedrockStatus, value);
    }

    public string BedrockStatusBrush
    {
        get => _bedrockStatusBrush;
        private set => SetProperty(ref _bedrockStatusBrush, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public async Task LoadAsync(string? focusedProviderKey = null, CancellationToken cancellationToken = default)
    {
        var openAi = await _providerAccountService.GetAsync("openai", cancellationToken);
        var ollama = await _providerAccountService.GetAsync("ollama", cancellationToken);
        var bedrock = await _providerAccountService.GetAsync("bedrock", cancellationToken);

        OpenAiApiKey = openAi?.Credential ?? string.Empty;
        OpenAiModelId = openAi?.DefaultModelId ?? "gpt-4.1-mini";
        OllamaEndpoint = ollama?.Endpoint ?? "http://localhost:11434";
        OllamaModelId = ollama?.DefaultModelId ?? string.Empty;
        BedrockRegion = bedrock?.Endpoint ?? "us-east-1";
        BedrockModelId = bedrock?.DefaultModelId ?? string.Empty;
        Status = string.Empty;
        SelectedProviderIndex = GetProviderIndex(focusedProviderKey);
        RefreshProviderStatuses();
    }

    private async Task SaveAsync()
    {
        if (!Validate())
        {
            return;
        }

        await _providerAccountService.SaveAsync(
            "openai",
            "OpenAI",
            "openai",
            endpoint: null,
            defaultModelId: OpenAiModelId.Trim(),
            credential: OpenAiApiKey.Trim(),
            isEnabled: true);

        await _providerAccountService.SaveAsync(
            "ollama",
            "Ollama",
            "ollama",
            endpoint: OllamaEndpoint.Trim(),
            defaultModelId: OllamaModelId.Trim(),
            credential: null,
            isEnabled: true);

        await _providerAccountService.SaveAsync(
            "bedrock",
            "AWS Bedrock",
            "bedrock",
            endpoint: BedrockRegion.Trim(),
            defaultModelId: BedrockModelId.Trim(),
            credential: null,
            isEnabled: true);

        _providerStatusService.Clear("openai");
        _providerStatusService.Clear("ollama");
        _providerStatusService.Clear("bedrock");
        RefreshProviderStatuses();
        Status = "Saved. Provider readiness updated.";
    }

    private bool Validate()
    {
        if (!string.IsNullOrWhiteSpace(OpenAiApiKey) && string.IsNullOrWhiteSpace(OpenAiModelId))
        {
            Status = "OpenAI model is required when an API key is set.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(OllamaEndpoint) &&
            (!Uri.TryCreate(OllamaEndpoint.Trim(), UriKind.Absolute, out var ollamaUri) ||
             (ollamaUri.Scheme != Uri.UriSchemeHttp && ollamaUri.Scheme != Uri.UriSchemeHttps)))
        {
            Status = "Ollama endpoint must be an absolute HTTP URL.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(BedrockModelId) && string.IsNullOrWhiteSpace(BedrockRegion))
        {
            Status = "Bedrock region is required when a model is set.";
            return false;
        }

        return true;
    }

    private async Task CheckOpenAiAsync()
    {
        await SetDiagnosticStatusAsync("openai", BuildOpenAiAccount());
    }

    private async Task CheckOllamaAsync()
    {
        await SetDiagnosticStatusAsync("ollama", BuildOllamaAccount());
    }

    private async Task CheckBedrockAsync()
    {
        await SetDiagnosticStatusAsync("bedrock", BuildBedrockAccount());
    }

    private void RefreshProviderStatuses()
    {
        SetProviderStatus("openai", EvaluateOpenAi());
        SetProviderStatus("ollama", EvaluateOllama());
        SetProviderStatus("bedrock", EvaluateBedrock());
    }

    private ProviderHealth EvaluateOpenAi()
    {
        return Evaluate(BuildAccount(
            "openai",
            "OpenAI",
            "openai",
            endpoint: null,
            defaultModelId: OpenAiModelId,
            credential: OpenAiApiKey));
    }

    private ProviderAccount BuildOpenAiAccount()
    {
        return BuildAccount(
            "openai",
            "OpenAI",
            "openai",
            endpoint: null,
            defaultModelId: OpenAiModelId,
            credential: OpenAiApiKey);
    }

    private ProviderHealth EvaluateOllama()
    {
        return Evaluate(BuildAccount(
            "ollama",
            "Ollama",
            "ollama",
            endpoint: OllamaEndpoint,
            defaultModelId: OllamaModelId,
            credential: null));
    }

    private ProviderAccount BuildOllamaAccount()
    {
        return BuildAccount(
            "ollama",
            "Ollama",
            "ollama",
            endpoint: OllamaEndpoint,
            defaultModelId: OllamaModelId,
            credential: null);
    }

    private ProviderHealth EvaluateBedrock()
    {
        return Evaluate(BuildAccount(
            "bedrock",
            "AWS Bedrock",
            "bedrock",
            endpoint: BedrockRegion,
            defaultModelId: BedrockModelId,
            credential: null));
    }

    private ProviderAccount BuildBedrockAccount()
    {
        return BuildAccount(
            "bedrock",
            "AWS Bedrock",
            "bedrock",
            endpoint: BedrockRegion,
            defaultModelId: BedrockModelId,
            credential: null);
    }

    private ProviderHealth Evaluate(ProviderAccount account)
    {
        return ProviderHealthEvaluator.EvaluateAccount(
            account,
            _registeredProviderKeys.Contains(account.Key));
    }

    private static ProviderAccount BuildAccount(
        string key,
        string displayName,
        string providerKind,
        string? endpoint,
        string? defaultModelId,
        string? credential)
    {
        return new ProviderAccount(
            Guid.Empty,
            key,
            displayName,
            providerKind,
            string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim(),
            string.IsNullOrWhiteSpace(defaultModelId) ? null : defaultModelId.Trim(),
            string.IsNullOrWhiteSpace(credential) ? null : credential.Trim(),
            isEnabled: true);
    }

    private void SetStatus(string providerKey, ProviderHealth health)
    {
        SetProviderStatus(providerKey, health);
        Status = $"{health.Label}: {health.Detail}";
    }

    private async Task SetDiagnosticStatusAsync(string providerKey, ProviderAccount account)
    {
        SetProviderStatus(providerKey, ProviderHealthEvaluator.EvaluateAccount(
            account,
            _registeredProviderKeys.Contains(account.Key)));
        Status = $"Checking {account.DisplayName}...";

        var result = await _providerDiagnosticsService.CheckAsync(
            account,
            _registeredProviderKeys.Contains(account.Key));
        SetProviderStatus(providerKey, result);
        Status = $"{result.Label}: {result.Detail}";
    }

    private void SetProviderStatus(string providerKey, ProviderHealth health)
    {
        var text = $"{health.Label}: {health.Detail}";
        var brush = GetStatusBrush(health);

        switch (providerKey)
        {
            case "openai":
                OpenAiStatus = text;
                OpenAiStatusBrush = brush;
                break;
            case "ollama":
                OllamaStatus = text;
                OllamaStatusBrush = brush;
                break;
            case "bedrock":
                BedrockStatus = text;
                BedrockStatusBrush = brush;
                break;
        }
    }

    private void SetProviderStatus(string providerKey, ProviderDiagnosticResult result)
    {
        var text = $"{result.Label}: {result.Detail}";
        var brush = GetStatusBrush(result);

        switch (providerKey)
        {
            case "openai":
                OpenAiStatus = text;
                OpenAiStatusBrush = brush;
                break;
            case "ollama":
                OllamaStatus = text;
                OllamaStatusBrush = brush;
                break;
            case "bedrock":
                BedrockStatus = text;
                BedrockStatusBrush = brush;
                break;
        }
    }

    private static string GetStatusBrush(ProviderHealth health)
    {
        return health.State switch
        {
            ProviderHealthState.Ready => "#16A34A",
            ProviderHealthState.NeedsSetup => "#D97706",
            ProviderHealthState.Disabled => "#6B7280",
            ProviderHealthState.MissingProvider => "#DC2626",
            _ => "#657085"
        };
    }

    private static string GetStatusBrush(ProviderDiagnosticResult result)
    {
        return result.State switch
        {
            ProviderDiagnosticState.Ready => "#16A34A",
            ProviderDiagnosticState.Configured => "#1C7C7D",
            ProviderDiagnosticState.SetupNeeded => "#D97706",
            ProviderDiagnosticState.Disabled => "#6B7280",
            ProviderDiagnosticState.MissingProvider => "#DC2626",
            ProviderDiagnosticState.Unreachable => "#DC2626",
            ProviderDiagnosticState.Unauthorized => "#DC2626",
            ProviderDiagnosticState.Error => "#DC2626",
            _ => "#657085"
        };
    }

    private static int GetProviderIndex(string? providerKey)
    {
        return providerKey?.ToLowerInvariant() switch
        {
            "ollama" => 1,
            "bedrock" => 2,
            _ => 0
        };
    }
}
