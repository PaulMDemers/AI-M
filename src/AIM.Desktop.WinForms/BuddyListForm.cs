using AIM.Core.Personalities;
using AIM.Core.PendingActions;
using AIM.Core.Providers;
using AIM.Core.Services;

namespace AIM.Desktop.WinForms;

internal sealed class BuddyListForm : Form
{
    private readonly IPersonalityService _personalityService;
    private readonly IProviderAccountService _providerAccountService;
    private readonly IProviderStatusService _providerStatusService;
    private readonly IReadOnlySet<string> _registeredProviderKeys;
    private readonly ChatWindowManager _chatWindowManager;
    private readonly IPendingAgentActionQueue _pendingAgentActionQueue;
    private readonly TreeView _buddyTree = new();
    private readonly Label _statusLabel = ClassicAim.Label("Online", ClassicAim.BoldFont, ClassicAim.AimBlue);
    private readonly Button _pendingButton = ClassicAim.Button("Pending AI Actions");
    private readonly Label _footerLabel = ClassicAim.Label("AI-M ready", ClassicAim.SmallFont, SystemColors.GrayText);
    private readonly NotifyIcon _trayIcon = new();
    private IReadOnlyList<FriendListItem> _friends = [];

    public BuddyListForm(
        IPersonalityService personalityService,
        IProviderAccountService providerAccountService,
        IProviderStatusService providerStatusService,
        IEnumerable<IAiProvider> providers,
        ChatWindowManager chatWindowManager,
        IPendingAgentActionQueue pendingAgentActionQueue)
    {
        _personalityService = personalityService;
        _providerAccountService = providerAccountService;
        _providerStatusService = providerStatusService;
        _registeredProviderKeys = providers
            .Select(provider => provider.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _chatWindowManager = chatWindowManager;
        _pendingAgentActionQueue = pendingAgentActionQueue;

        ClassicAim.ApplyClassicForm(this);
        Text = "AI-M Buddy List";
        Width = 278;
        Height = 640;
        MinimumSize = new Size(248, 460);
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;

        BuildUi();
        InitializeTray();
        _providerStatusService.ProviderStatusChanged += OnProviderStatusChanged;
        _pendingAgentActionQueue.ActionsChanged += OnPendingActionsChanged;

        Shown += async (_, _) => await LoadBuddiesAsync();
        FormClosing += OnFormClosing;
    }

    private void BuildUi()
    {
        var menu = new MenuStrip
        {
            Font = ClassicAim.UiFont,
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System
        };
        var myAim = new ToolStripMenuItem("My AI-M");
        myAim.DropDownItems.Add("Refresh List", null, async (_, _) => await LoadBuddiesAsync());
        myAim.DropDownItems.Add("Check Providers", null, async (_, _) => await CheckProvidersAsync());
        myAim.DropDownItems.Add("Pending AI Actions", null, (_, _) => OpenPendingActions());
        myAim.DropDownItems.Add("Set Away", null, (_, _) => SetFooter("Away message is not configured yet."));
        myAim.DropDownItems.Add("-");
        myAim.DropDownItems.Add("Sign Off", null, (_, _) => Close());
        var people = new ToolStripMenuItem("People");
        people.DropDownItems.Add("Send Instant Message", null, async (_, _) => await OpenSelectedChatAsync());
        var help = new ToolStripMenuItem("Help");
        help.DropDownItems.Add("About AI-M", null, (_, _) => MessageBox.Show(
            this,
            "AI-M WinForms shell\nClassic AIM-inspired desktop for AI personalities.",
            "About AI-M",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information));
        menu.Items.AddRange([myAim, people, new ToolStripMenuItem("View"), help]);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 6,
            ColumnCount = 1,
            Padding = new Padding(4, 0, 4, 4)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var banner = new Panel
        {
            Height = 54,
            Dock = DockStyle.Fill,
            BackColor = ClassicAim.AwayYellow,
            BorderStyle = BorderStyle.Fixed3D,
            Padding = new Padding(6, 4, 6, 4)
        };
        var bannerTitle = ClassicAim.Label("AI-M Instant Messenger", ClassicAim.BoldFont, ClassicAim.AimBlue);
        bannerTitle.Location = new Point(6, 6);
        var bannerText = ClassicAim.Label("Managing AI personalities", ClassicAim.SmallFont);
        bannerText.Location = new Point(6, 26);
        banner.Controls.AddRange([bannerTitle, bannerText]);

        var statusPanel = new Panel
        {
            Height = 26,
            Dock = DockStyle.Fill,
            Padding = new Padding(2, 4, 0, 0)
        };
        statusPanel.Controls.Add(_statusLabel);

        _pendingButton.Dock = DockStyle.Fill;
        _pendingButton.Height = 24;
        _pendingButton.BackColor = Color.FromArgb(255, 250, 214);
        _pendingButton.Visible = false;
        _pendingButton.Click += (_, _) => OpenPendingActions();

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = ClassicAim.UiFont
        };
        var onlinePage = new TabPage("Online") { BackColor = SystemColors.Control };
        var setupPage = new TabPage("List Setup") { BackColor = SystemColors.Control };

        _buddyTree.Dock = DockStyle.Fill;
        _buddyTree.BorderStyle = BorderStyle.Fixed3D;
        _buddyTree.Font = ClassicAim.UiFont;
        _buddyTree.HideSelection = false;
        _buddyTree.ShowNodeToolTips = true;
        _buddyTree.ImageList = ClassicAim.CreateBuddyImages();
        _buddyTree.NodeMouseDoubleClick += async (_, _) => await OpenSelectedChatAsync();
        onlinePage.Controls.Add(_buddyTree);

        var setupLabel = ClassicAim.Label("Use the WPF shell for detailed personality templates, memory review, and provider setup.", ClassicAim.SmallFont);
        setupLabel.MaximumSize = new Size(220, 0);
        setupLabel.Location = new Point(8, 10);
        setupPage.Controls.Add(setupLabel);
        tabs.TabPages.AddRange([onlinePage, setupPage]);

        var footerPanel = new TableLayoutPanel
        {
            RowCount = 2,
            ColumnCount = 1,
            Height = 58,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 3, 0, 0)
        };
        footerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        footerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        var statusBox = ClassicAim.SunkenPanel();
        statusBox.Dock = DockStyle.Fill;
        statusBox.Padding = new Padding(5, 2, 4, 0);
        _footerLabel.Dock = DockStyle.Fill;
        _footerLabel.AutoSize = false;
        _footerLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusBox.Controls.Add(_footerLabel);

        var buttons = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 0)
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        var imButton = ClassicAim.Button("IM");
        imButton.Click += async (_, _) => await OpenSelectedChatAsync();
        var infoButton = ClassicAim.Button("Info");
        infoButton.Click += (_, _) => ShowSelectedInfo();
        buttons.Controls.Add(imButton, 0, 0);
        buttons.Controls.Add(infoButton, 1, 0);
        footerPanel.Controls.Add(statusBox, 0, 0);
        footerPanel.Controls.Add(buttons, 0, 1);

        root.Controls.Add(menu, 0, 0);
        root.Controls.Add(banner, 0, 1);
        root.Controls.Add(statusPanel, 0, 2);
        root.Controls.Add(_pendingButton, 0, 3);
        root.Controls.Add(tabs, 0, 4);
        root.Controls.Add(footerPanel, 0, 5);
        Controls.Add(root);
        MainMenuStrip = menu;
        UpdatePendingIndicator();
    }

    private async Task LoadBuddiesAsync()
    {
        var personalities = await _personalityService.ListAsync();
        var providerAccounts = (await _providerAccountService.ListAsync())
            .ToDictionary(account => account.Key, StringComparer.OrdinalIgnoreCase);
        _friends = personalities
            .Select(personality =>
            {
                providerAccounts.TryGetValue(personality.ProviderKey, out var account);
                var health = ProviderHealthEvaluator.Evaluate(
                    personality,
                    account,
                    _registeredProviderKeys.Contains(personality.ProviderKey));
                var diagnostic = _providerStatusService.GetCached(personality.ProviderKey);
                return new FriendListItem(personality, health, diagnostic);
            })
            .ToArray();
        _buddyTree.BeginUpdate();
        _buddyTree.Nodes.Clear();

        foreach (var category in _friends
            .GroupBy(friend => friend.Personality.Category)
            .OrderBy(group => CategorySortOrder(group.Key))
            .ThenBy(group => group.Key))
        {
            var group = new TreeNode($"{category.Key} ({category.Count()})")
            {
                ImageKey = "group",
                SelectedImageKey = "group"
            };

            foreach (var friend in category.OrderBy(friend => friend.Personality.DisplayName))
            {
                var personality = friend.Personality;
                var imageKey = _buddyTree.ImageList is not null &&
                    ClassicAim.TryAddAvatarImage(_buddyTree.ImageList, personality)
                    ? ClassicAim.GetAvatarImageKey(personality)
                    : "online";
                group.Nodes.Add(new TreeNode(friend.DisplayText)
                {
                    Tag = friend,
                    ImageKey = imageKey,
                    SelectedImageKey = imageKey,
                    ToolTipText = $"{personality.Status}\n{friend.PresenceDetail}",
                    ForeColor = GetPresenceColor(friend)
                });
            }

            _buddyTree.Nodes.Add(group);
            group.Expand();
        }

        var ready = _friends.Count(friend => friend.IsReady);
        _statusLabel.Text = $"Online ({ready}/{_friends.Count})";
        SetFooter($"{ready} ready, {_friends.Count - ready} need setup");
        _buddyTree.EndUpdate();
    }

    private async Task CheckProvidersAsync()
    {
        SetFooter("Checking providers...");
        var diagnostics = await _providerStatusService.RefreshAllAsync();
        await LoadBuddiesAsync();
        SetFooter(BuildProviderSummary(diagnostics.Values));
    }

    private void OnProviderStatusChanged(object? sender, ProviderStatusChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => _ = RefreshFromProviderStatusAsync()));
            return;
        }

        _ = RefreshFromProviderStatusAsync();
    }

    private async Task RefreshFromProviderStatusAsync()
    {
        await LoadBuddiesAsync();
        var snapshot = _providerStatusService.Snapshot();
        SetFooter(snapshot.Count == 0 ? "Provider checks not run" : BuildProviderSummary(snapshot.Values));
    }

    private static int CategorySortOrder(string category)
    {
        return category switch
        {
            "Core" => 0,
            "Archetypes" => 1,
            "Demo Figures" => 2,
            "Providers" => 3,
            "My Contacts" => 4,
            _ => 5
        };
    }

    private async Task OpenSelectedChatAsync()
    {
        if (_buddyTree.SelectedNode?.Tag is FriendListItem friend)
        {
            await _chatWindowManager.OpenAsync(friend.Personality);
        }
    }

    private void ShowSelectedInfo()
    {
        if (_buddyTree.SelectedNode?.Tag is not FriendListItem friend)
        {
            SetFooter("Select a buddy first.");
            return;
        }

        MessageBox.Show(
            this,
            $"{friend.Personality.DisplayName}\n{friend.Personality.Status}\n{friend.Personality.Category}\n{friend.PresenceDetail}",
            "Buddy Info",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static Color GetPresenceColor(FriendListItem friend)
    {
        if (friend.Diagnostic is not null)
        {
            return friend.Diagnostic.State switch
            {
                ProviderDiagnosticState.Ready => SystemColors.WindowText,
                ProviderDiagnosticState.Configured => Color.FromArgb(0, 90, 90),
                ProviderDiagnosticState.SetupNeeded => Color.FromArgb(150, 88, 0),
                ProviderDiagnosticState.Disabled => SystemColors.GrayText,
                ProviderDiagnosticState.MissingProvider => Color.FromArgb(150, 0, 0),
                ProviderDiagnosticState.Unreachable => Color.FromArgb(150, 0, 0),
                ProviderDiagnosticState.Unauthorized => Color.FromArgb(150, 0, 0),
                ProviderDiagnosticState.Error => Color.FromArgb(150, 0, 0),
                _ => SystemColors.WindowText
            };
        }

        return friend.Health.State switch
        {
            ProviderHealthState.Ready => SystemColors.WindowText,
            ProviderHealthState.NeedsSetup => Color.FromArgb(150, 88, 0),
            ProviderHealthState.Disabled => SystemColors.GrayText,
            ProviderHealthState.MissingProvider => Color.FromArgb(150, 0, 0),
            _ => SystemColors.WindowText
        };
    }

    private static string BuildProviderSummary(IEnumerable<ProviderDiagnosticResult> diagnostics)
    {
        var results = diagnostics.ToArray();

        if (results.Length == 0)
        {
            return "No providers found";
        }

        var verified = results.Count(result => result.State == ProviderDiagnosticState.Ready);
        var configured = results.Count(result => result.State == ProviderDiagnosticState.Configured);
        var needsAttention = results.Length - verified - configured;

        return needsAttention == 0
            ? $"{verified} verified, {configured} configured"
            : $"{verified} verified, {configured} configured, {needsAttention} need attention";
    }

    private void SetFooter(string text)
    {
        _footerLabel.Text = text;
    }

    private void OpenPendingActions()
    {
        using var review = new PendingActionsReviewForm(_pendingAgentActionQueue);
        review.ShowDialog(this);
        UpdatePendingIndicator();
    }

    private void OnPendingActionsChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(UpdatePendingIndicator));
            return;
        }

        UpdatePendingIndicator();
    }

    private void UpdatePendingIndicator()
    {
        var count = _pendingAgentActionQueue.Actions.Count;
        _pendingButton.Text = count == 1
            ? "1 AI action needs review"
            : $"{count} AI actions need review";
        _pendingButton.Visible = count > 0;
    }

    private void InitializeTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Buddy List", null, (_, _) =>
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        });
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _reallyClose = true;
            Close();
        });

        _trayIcon.Icon = SystemIcons.Application;
        _trayIcon.Text = "AI-M";
        _trayIcon.Visible = true;
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) =>
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        };
    }

    private bool _reallyClose;

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_reallyClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _trayIcon.Dispose();
        _providerStatusService.ProviderStatusChanged -= OnProviderStatusChanged;
        _pendingAgentActionQueue.ActionsChanged -= OnPendingActionsChanged;
    }
}
