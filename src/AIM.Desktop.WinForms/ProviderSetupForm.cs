using AIM.Core.Providers;
using AIM.Core.Services;

namespace AIM.Desktop.WinForms;

internal sealed class ProviderSetupForm : Form
{
    private readonly string _providerKey;
    private readonly IProviderAccountService _providerAccountService;
    private readonly IProviderDiagnosticsService _providerDiagnosticsService;
    private readonly IProviderStatusService _providerStatusService;
    private readonly bool _providerRegistered;
    private readonly Label _status = ClassicAim.Label(string.Empty, ClassicAim.SmallFont, Color.FromArgb(120, 60, 0));
    private readonly TextBox _firstValue = new();
    private readonly TextBox _model = new();

    public ProviderSetupForm(
        string providerKey,
        IProviderAccountService providerAccountService,
        IProviderDiagnosticsService providerDiagnosticsService,
        IProviderStatusService providerStatusService,
        bool providerRegistered)
    {
        _providerKey = providerKey;
        _providerAccountService = providerAccountService;
        _providerDiagnosticsService = providerDiagnosticsService;
        _providerStatusService = providerStatusService;
        _providerRegistered = providerRegistered;

        ClassicAim.ApplyClassicForm(this);
        Text = $"{GetDisplayName()} Setup";
        Width = 430;
        Height = 230;
        MinimumSize = new Size(380, 210);
        BuildUi();
        Load += async (_, _) => await LoadProviderAsync();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 2,
            Padding = new Padding(10)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = ClassicAim.Label($"{GetDisplayName()} Provider", ClassicAim.BoldFont, ClassicAim.AimBlue);
        title.Dock = DockStyle.Fill;
        root.Controls.Add(title, 0, 0);
        root.SetColumnSpan(title, 2);

        var firstLabel = ClassicAim.Label(GetFirstLabel(), ClassicAim.UiFont);
        firstLabel.Dock = DockStyle.Fill;
        firstLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(firstLabel, 0, 1);

        _firstValue.Dock = DockStyle.Fill;
        _firstValue.Font = ClassicAim.UiFont;
        _firstValue.PasswordChar = string.Equals(_providerKey, "openai", StringComparison.OrdinalIgnoreCase) ? '*' : '\0';
        root.Controls.Add(_firstValue, 1, 1);

        var modelLabel = ClassicAim.Label("Model", ClassicAim.UiFont);
        modelLabel.Dock = DockStyle.Fill;
        modelLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(modelLabel, 0, 2);

        _model.Dock = DockStyle.Fill;
        _model.Font = ClassicAim.UiFont;
        root.Controls.Add(_model, 1, 2);

        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(_status, 0, 3);
        root.SetColumnSpan(_status, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        var save = ClassicAim.Button("Save");
        save.Width = 80;
        save.Click += async (_, _) => await SaveAsync();
        var cancel = ClassicAim.Button("Cancel");
        cancel.Width = 80;
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 4);
        root.SetColumnSpan(buttons, 2);

        Controls.Add(root);
    }

    private async Task LoadProviderAsync()
    {
        var account = await _providerAccountService.GetAsync(_providerKey);

        switch (_providerKey.ToLowerInvariant())
        {
            case "openai":
                _firstValue.Text = account?.Credential ?? string.Empty;
                _model.Text = account?.DefaultModelId ?? "gpt-4.1-mini";
                break;
            case "ollama":
                _firstValue.Text = account?.Endpoint ?? "http://localhost:11434";
                _model.Text = account?.DefaultModelId ?? string.Empty;
                break;
            case "bedrock":
                _firstValue.Text = account?.Endpoint ?? "us-east-1";
                _model.Text = account?.DefaultModelId ?? string.Empty;
                break;
        }

        RefreshStatus();
    }

    private async Task SaveAsync()
    {
        if (!ValidateInputs())
        {
            return;
        }

        var providerKind = _providerKey.ToLowerInvariant();
        await _providerAccountService.SaveAsync(
            _providerKey,
            GetDisplayName(),
            providerKind,
            endpoint: providerKind == "openai" ? null : _firstValue.Text.Trim(),
            defaultModelId: _model.Text.Trim(),
            credential: providerKind == "openai" ? _firstValue.Text.Trim() : null,
            isEnabled: true);

        _status.Text = $"Checking {GetDisplayName()}...";
        var result = await _providerDiagnosticsService.CheckAsync(BuildAccount(), _providerRegistered);
        _status.Text = $"{result.Label}: {result.Detail}";

        if (result.IsUsable)
        {
            await _providerStatusService.RefreshAsync(_providerKey);
            DialogResult = DialogResult.OK;
        }
    }

    private bool ValidateInputs()
    {
        var providerKind = _providerKey.ToLowerInvariant();

        if (providerKind == "openai" && string.IsNullOrWhiteSpace(_firstValue.Text))
        {
            _status.Text = "OpenAI needs an API key.";
            return false;
        }

        if (providerKind == "ollama" &&
            (string.IsNullOrWhiteSpace(_firstValue.Text) ||
             !Uri.TryCreate(_firstValue.Text.Trim(), UriKind.Absolute, out var uri) ||
             (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            _status.Text = "Ollama endpoint must be an absolute HTTP URL.";
            return false;
        }

        if (providerKind == "bedrock" && string.IsNullOrWhiteSpace(_firstValue.Text))
        {
            _status.Text = "Bedrock needs an AWS region.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_model.Text))
        {
            _status.Text = $"{GetDisplayName()} needs a model.";
            return false;
        }

        return true;
    }

    private void RefreshStatus()
    {
        var health = ProviderHealthEvaluator.EvaluateAccount(BuildAccount(), _providerRegistered);
        _status.Text = $"{health.Label}: {health.Detail}";
    }

    private ProviderAccount BuildAccount()
    {
        var providerKind = _providerKey.ToLowerInvariant();
        return new ProviderAccount(
            Guid.Empty,
            _providerKey,
            GetDisplayName(),
            providerKind,
            providerKind == "openai" ? null : _firstValue.Text,
            _model.Text,
            providerKind == "openai" ? _firstValue.Text : null,
            isEnabled: true);
    }

    private string GetDisplayName()
    {
        return _providerKey.ToLowerInvariant() switch
        {
            "openai" => "OpenAI",
            "ollama" => "Ollama",
            "bedrock" => "AWS Bedrock",
            _ => _providerKey
        };
    }

    private string GetFirstLabel()
    {
        return _providerKey.ToLowerInvariant() switch
        {
            "openai" => "API Key",
            "ollama" => "Endpoint",
            "bedrock" => "Region",
            _ => "Setting"
        };
    }
}
