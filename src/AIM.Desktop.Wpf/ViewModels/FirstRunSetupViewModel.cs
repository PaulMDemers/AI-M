using AIM.Core.Providers;
using AIM.Core.Services;
using AIM.Desktop.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class FirstRunSetupViewModel : ObservableObject
{
    private readonly IProviderAccountService _providerAccountService;
    private readonly IProviderDiagnosticsService _providerDiagnosticsService;
    private readonly IProviderStatusService _providerStatusService;
    private readonly IReadOnlySet<string> _registeredProviderKeys;
    private readonly FirstRunSetupPreferenceService _preferenceService;
    private string _openAiApiKey = string.Empty;
    private string _openAiModelId = "gpt-4.1-mini";
    private string _ollamaEndpoint = "http://localhost:11434";
    private string _ollamaModelId = string.Empty;
    private string _bedrockRegion = "us-east-1";
    private string _bedrockModelId = string.Empty;
    private string _openAiStatus = string.Empty;
    private string _openAiStatusBrush = "#657085";
    private string _ollamaStatus = string.Empty;
    private string _ollamaStatusBrush = "#657085";
    private string _bedrockStatus = string.Empty;
    private string _bedrockStatusBrush = "#657085";
    private string _status = "Choose a provider to bring the AI contacts online.";
    private int _selectedProviderIndex;

    public FirstRunSetupViewModel(
        IProviderAccountService providerAccountService,
        IProviderDiagnosticsService providerDiagnosticsService,
        IProviderStatusService providerStatusService,
        IEnumerable<IAiProvider> providers,
        FirstRunSetupPreferenceService preferenceService)
    {
        _providerAccountService = providerAccountService;
        _providerDiagnosticsService = providerDiagnosticsService;
        _providerStatusService = providerStatusService;
        _registeredProviderKeys = providers
            .Select(provider => provider.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _preferenceService = preferenceService;
        SaveSelectedProviderCommand = new AsyncRelayCommand(SaveSelectedProviderAsync);
        ContinueWithDemoCommand = new AsyncRelayCommand(ContinueWithDemoAsync);
    }

    public event EventHandler? RequestClose;

    public IAsyncRelayCommand SaveSelectedProviderCommand { get; }

    public IAsyncRelayCommand ContinueWithDemoCommand { get; }

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

    public async Task LoadAsync(CancellationToken cancellationToken = default)
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
        RefreshStatuses();
    }

    private async Task SaveSelectedProviderAsync()
    {
        if (!ValidateSelectedProvider())
        {
            return;
        }

        switch (SelectedProviderIndex)
        {
            case 1:
                await SaveOllamaAsync();
                break;
            case 2:
                await SaveBedrockAsync();
                break;
            default:
                await SaveOpenAiAsync();
                break;
        }

        RefreshStatuses();
        Status = "Checking provider...";
        var result = await CheckSelectedProviderAsync();
        Status = result.IsUsable
            ? $"{result.Detail} Contacts are ready."
            : $"{result.Label}: {result.Detail}";

        if (result.IsUsable)
        {
            await _preferenceService.SetUseDemoModeAsync(false);
            await _providerStatusService.RefreshAsync(GetSelectedProviderKey());
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task ContinueWithDemoAsync()
    {
        await _preferenceService.SetUseDemoModeAsync(true);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveOpenAiAsync()
    {
        await _providerAccountService.SaveAsync(
            "openai",
            "OpenAI",
            "openai",
            endpoint: null,
            defaultModelId: OpenAiModelId.Trim(),
            credential: OpenAiApiKey.Trim(),
            isEnabled: true);
    }

    private async Task SaveOllamaAsync()
    {
        await _providerAccountService.SaveAsync(
            "ollama",
            "Ollama",
            "ollama",
            endpoint: OllamaEndpoint.Trim(),
            defaultModelId: OllamaModelId.Trim(),
            credential: null,
            isEnabled: true);
    }

    private async Task SaveBedrockAsync()
    {
        await _providerAccountService.SaveAsync(
            "bedrock",
            "AWS Bedrock",
            "bedrock",
            endpoint: BedrockRegion.Trim(),
            defaultModelId: BedrockModelId.Trim(),
            credential: null,
            isEnabled: true);
    }

    private bool ValidateSelectedProvider()
    {
        return SelectedProviderIndex switch
        {
            1 => ValidateOllama(),
            2 => ValidateBedrock(),
            _ => ValidateOpenAi()
        };
    }

    private bool ValidateOpenAi()
    {
        if (string.IsNullOrWhiteSpace(OpenAiApiKey))
        {
            Status = "OpenAI needs an API key.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(OpenAiModelId))
        {
            Status = "OpenAI needs a model.";
            return false;
        }

        return true;
    }

    private bool ValidateOllama()
    {
        if (string.IsNullOrWhiteSpace(OllamaEndpoint) ||
            !Uri.TryCreate(OllamaEndpoint.Trim(), UriKind.Absolute, out var ollamaUri) ||
            (ollamaUri.Scheme != Uri.UriSchemeHttp && ollamaUri.Scheme != Uri.UriSchemeHttps))
        {
            Status = "Ollama endpoint must be an absolute HTTP URL.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(OllamaModelId))
        {
            Status = "Ollama needs a local model name.";
            return false;
        }

        return true;
    }

    private bool ValidateBedrock()
    {
        if (string.IsNullOrWhiteSpace(BedrockRegion))
        {
            Status = "Bedrock needs an AWS region.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(BedrockModelId))
        {
            Status = "Bedrock needs a model id.";
            return false;
        }

        return true;
    }

    private void RefreshStatuses()
    {
        SetProviderStatus("openai", EvaluateOpenAi());
        SetProviderStatus("ollama", EvaluateOllama());
        SetProviderStatus("bedrock", EvaluateBedrock());
    }

    private ProviderHealth EvaluateSelectedProvider()
    {
        return SelectedProviderIndex switch
        {
            1 => EvaluateOllama(),
            2 => EvaluateBedrock(),
            _ => EvaluateOpenAi()
        };
    }

    private Task<ProviderDiagnosticResult> CheckSelectedProviderAsync()
    {
        return SelectedProviderIndex switch
        {
            1 => CheckProviderAsync("ollama", BuildOllamaAccount()),
            2 => CheckProviderAsync("bedrock", BuildBedrockAccount()),
            _ => CheckProviderAsync("openai", BuildOpenAiAccount())
        };
    }

    private string GetSelectedProviderKey()
    {
        return SelectedProviderIndex switch
        {
            1 => "ollama",
            2 => "bedrock",
            _ => "openai"
        };
    }

    private async Task<ProviderDiagnosticResult> CheckProviderAsync(string providerKey, ProviderAccount account)
    {
        var result = await _providerDiagnosticsService.CheckAsync(
            account,
            _registeredProviderKeys.Contains(account.Key));
        SetProviderStatus(providerKey, result);
        return result;
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
}
