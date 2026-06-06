using System.Net.Http;
using System.Text.Json.Nodes;
using AIM.Core.Chat;
using AIM.Core.PendingActions;
using AIM.Core.Personalities;
using AIM.Core.Providers;
using AIM.Core.Services;
using AIM.Core.Tools;

namespace AIM.Desktop.WinForms;

internal sealed class ChatForm : Form
{
    private readonly IConversationService _conversationService;
    private readonly IPersonalityService _personalityService;
    private readonly IProviderAccountService _providerAccountService;
    private readonly IProviderDiagnosticsService _providerDiagnosticsService;
    private readonly IProviderStatusService _providerStatusService;
    private readonly IMemoryService _memoryService;
    private readonly IMemorySuggestionService _memorySuggestionService;
    private readonly IChatContextBuilder _chatContextBuilder;
    private readonly IAgentToolRegistry _toolRegistry;
    private readonly IPendingAgentActionQueue _pendingAgentActionQueue;
    private readonly IReadOnlyDictionary<string, IAiProvider> _providers;
    private readonly RichTextBox _transcript = new();
    private readonly TextBox _input = new();
    private readonly Label _status = ClassicAim.Label("Ready", ClassicAim.SmallFont, SystemColors.GrayText);
    private readonly Label _summary = ClassicAim.Label("Summary: none", ClassicAim.SmallFont, SystemColors.GrayText);
    private readonly Panel _providerSetupPanel = new();
    private readonly Label _providerSetupLabel = ClassicAim.Label(string.Empty, ClassicAim.SmallFont, Color.FromArgb(120, 60, 0));
    private readonly FlowLayoutPanel _approvalPanel = new();
    private readonly Button _stopButton = ClassicAim.Button("Stop");
    private CancellationTokenSource? _responseCancellationTokenSource;
    private Conversation? _conversation;
    private ProviderHealth? _providerHealth;
    private bool _isBusy;

    public ChatForm(
        Personality personality,
        IConversationService conversationService,
        IPersonalityService personalityService,
        IProviderAccountService providerAccountService,
        IProviderDiagnosticsService providerDiagnosticsService,
        IProviderStatusService providerStatusService,
        IMemoryService memoryService,
        IMemorySuggestionService memorySuggestionService,
        IChatContextBuilder chatContextBuilder,
        IAgentToolRegistry toolRegistry,
        IPendingAgentActionQueue pendingAgentActionQueue,
        IEnumerable<IAiProvider> providers)
    {
        Personality = personality;
        _conversationService = conversationService;
        _personalityService = personalityService;
        _providerAccountService = providerAccountService;
        _providerDiagnosticsService = providerDiagnosticsService;
        _providerStatusService = providerStatusService;
        _memoryService = memoryService;
        _memorySuggestionService = memorySuggestionService;
        _chatContextBuilder = chatContextBuilder;
        _toolRegistry = toolRegistry;
        _pendingAgentActionQueue = pendingAgentActionQueue;
        _providers = providers.ToDictionary(provider => provider.Key, StringComparer.OrdinalIgnoreCase);

        ClassicAim.ApplyClassicForm(this);
        Text = BuildWindowTitle(personality);
        Width = 560;
        Height = 620;
        MinimumSize = new Size(480, 460);
        BuildUi();
        _providerStatusService.ProviderStatusChanged += OnProviderStatusChanged;
        FormClosed += (_, _) => _providerStatusService.ProviderStatusChanged -= OnProviderStatusChanged;
    }

    public Personality Personality { get; private set; }

    public async Task LoadConversationAsync()
    {
        await RefreshProviderHealthAsync();
        var group = await _conversationService.GetOrCreateConversationGroupAsync(Personality.Id);
        _conversation = await _conversationService.GetOrCreateConversationAsync(Personality.Id, group.Id);
        await RefreshConversationHeaderAsync();

        var messages = await _conversationService.GetMessagesAsync(_conversation.Id);

        foreach (var message in messages.Where(message => message.Role is ChatRole.User or ChatRole.Assistant))
        {
            AppendTranscript(message.Role == ChatRole.User ? "You" : Personality.DisplayName, message.Content, message.Role == ChatRole.User);
        }

        AppendSetupNoticeIfNeeded();
    }

    private void BuildUi()
    {
        var menu = new MenuStrip
        {
            Font = ClassicAim.UiFont,
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System
        };
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("Close", null, (_, _) => Close());
        var edit = new ToolStripMenuItem("Edit");
        edit.DropDownItems.Add("Copy", null, (_, _) => _transcript.Copy());
        var conversation = new ToolStripMenuItem("Conversation");
        conversation.DropDownItems.Add("Refresh Summary", null, async (_, _) => await QuietSummaryRefreshAsync());
        var people = new ToolStripMenuItem("People");
        people.DropDownItems.Add("Get Info", null, (_, _) => ShowBuddyInfo());
        menu.Items.AddRange([file, edit, conversation, people, new ToolStripMenuItem("Help")]);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(6, 0, 6, 6)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 2,
            Padding = new Padding(0, 6, 0, 0)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var avatarRail = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Control,
            Padding = new Padding(0, 0, 8, 0)
        };
        var avatar = ClassicAim.AvatarPicture(Personality, 40);
        avatar.Location = new Point(4, 0);
        avatarRail.Controls.Add(avatar);

        _summary.Dock = DockStyle.Fill;
        _summary.Height = 24;
        _summary.AutoEllipsis = true;

        _providerSetupPanel.Dock = DockStyle.Fill;
        _providerSetupPanel.Height = 34;
        _providerSetupPanel.BackColor = Color.FromArgb(255, 248, 229);
        _providerSetupPanel.BorderStyle = BorderStyle.Fixed3D;
        _providerSetupPanel.Padding = new Padding(5, 4, 5, 4);
        _providerSetupPanel.Visible = false;
        _providerSetupLabel.AutoSize = false;
        _providerSetupLabel.Dock = DockStyle.Fill;
        _providerSetupLabel.TextAlign = ContentAlignment.MiddleLeft;
        var setupProviderButton = ClassicAim.Button("Setup Provider");
        setupProviderButton.Dock = DockStyle.Right;
        setupProviderButton.Width = 112;
        setupProviderButton.Click += async (_, _) => await ShowProviderSetupAsync();
        _providerSetupPanel.Controls.Add(setupProviderButton);
        _providerSetupPanel.Controls.Add(_providerSetupLabel);

        _transcript.Dock = DockStyle.Fill;
        _transcript.Font = new Font("Tahoma", 9f);
        _transcript.BackColor = SystemColors.Window;
        _transcript.BorderStyle = BorderStyle.Fixed3D;
        _transcript.ReadOnly = true;
        _transcript.DetectUrls = true;

        _approvalPanel.Dock = DockStyle.Fill;
        _approvalPanel.AutoSize = true;
        _approvalPanel.FlowDirection = FlowDirection.TopDown;
        _approvalPanel.WrapContents = false;
        _approvalPanel.Visible = false;

        _input.Dock = DockStyle.Fill;
        _input.Multiline = true;
        _input.AcceptsReturn = true;
        _input.ScrollBars = ScrollBars.Vertical;
        _input.BorderStyle = BorderStyle.Fixed3D;
        _input.Font = new Font("Tahoma", 9f);
        _input.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                await SendAsync();
            }
        };

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 38,
            ColumnCount = 3
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        var send = ClassicAim.Button("Send");
        send.Dock = DockStyle.Fill;
        send.Margin = new Padding(4, 4, 0, 0);
        send.Click += async (_, _) => await SendAsync();
        _stopButton.Dock = DockStyle.Fill;
        _stopButton.Margin = new Padding(4, 4, 4, 0);
        _stopButton.Click += (_, _) => _responseCancellationTokenSource?.Cancel();
        _stopButton.Enabled = false;
        buttons.Controls.Add(_status, 0, 0);
        buttons.Controls.Add(_stopButton, 1, 0);
        buttons.Controls.Add(send, 2, 0);

        root.Controls.Add(menu, 0, 0);
        root.Controls.Add(body, 0, 1);

        body.Controls.Add(_providerSetupPanel, 0, 0);
        body.SetColumnSpan(_providerSetupPanel, 2);
        body.Controls.Add(avatarRail, 0, 1);
        body.SetRowSpan(avatarRail, 4);
        body.Controls.Add(_transcript, 1, 1);
        body.Controls.Add(_approvalPanel, 1, 2);
        body.Controls.Add(_input, 1, 3);
        body.Controls.Add(buttons, 1, 4);

        Controls.Add(root);
        MainMenuStrip = menu;
    }

    private async Task SendAsync()
    {
        var text = _input.Text.Trim();

        if (string.IsNullOrWhiteSpace(text) || _isBusy || _conversation is null)
        {
            return;
        }

        await RefreshProviderHealthAsync();

        if (HasProviderSetupIssue)
        {
            AppendSetupNoticeIfNeeded();
            return;
        }

        _input.Clear();
        await SendVisibleMessageAsync(text);
    }

    private void ShowBuddyInfo()
    {
        MessageBox.Show(
            this,
            $"{Personality.DisplayName}\n{Personality.Status}\n{Personality.Category}",
            "Buddy Info",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static string BuildWindowTitle(Personality personality)
    {
        return $"Instant Message with {personality.DisplayName} - AI-M";
    }

    private async Task SendVisibleMessageAsync(string text)
    {
        if (_conversation is null)
        {
            return;
        }

        SetBusy(true, $"{Personality.DisplayName} is typing...");

        try
        {
            var userMessage = await _conversationService.AddMessageAsync(_conversation.Id, ChatRole.User, text);
            AppendTranscript("You", userMessage.Content, isUser: true);

            if (!TryResolveProvider(out var provider))
            {
                var content = $"Provider '{Personality.ProviderKey}' is not configured.";
                await _conversationService.AddMessageAsync(_conversation.Id, ChatRole.Assistant, content);
                AppendTranscript(Personality.DisplayName, content, isUser: false);
                return;
            }

            var existingMessages = await _conversationService.GetMessagesAsync(_conversation.Id);
            var context = await _chatContextBuilder.BuildAsync(Personality, _conversation);
            var requestMessages = ChatRequestMessageWindow.Select(existingMessages, context);
            var request = new ChatRequest(Personality, _conversation, requestMessages, context);
            var assistantContent = await RunProviderTurnAsync(provider, request, requestMessages, context);

            if (!string.IsNullOrWhiteSpace(assistantContent))
            {
                AppendTranscript(Personality.DisplayName, assistantContent, isUser: false);
                await _conversationService.AddMessageAsync(_conversation.Id, ChatRole.Assistant, assistantContent);
                await _memorySuggestionService.SuggestFromTurnAsync(
                    Personality.Id,
                    _conversation.Id,
                    text,
                    assistantContent);
            }
        }
        finally
        {
            SetBusy(false, "Ready");
        }
    }

    private async Task QuietSummaryRefreshAsync()
    {
        if (_conversation is null || _isBusy)
        {
            return;
        }

        SetBusy(true, "Refreshing summary...");

        try
        {
            await RefreshProviderHealthAsync();

            if (HasProviderSetupIssue)
            {
                AppendSetupNoticeIfNeeded();
                return;
            }

            if (!TryResolveProvider(out var provider))
            {
                AppendTranscript("AI-M", $"Provider '{Personality.ProviderKey}' is not configured.", isUser: false);
                return;
            }

            var existingMessages = await _conversationService.GetMessagesAsync(_conversation.Id);
            var context = await _chatContextBuilder.BuildAsync(Personality, _conversation);
            var requestMessages = ChatRequestMessageWindow
                .Select(existingMessages, context)
                .Append(new ChatMessage(
                    Guid.NewGuid(),
                    _conversation.Id,
                    ChatRole.System,
                    "Quiet app request: refresh the durable summary for this conversation. Prefer requesting conversation.summary.update if the summary should change. Do not produce user-facing text unless necessary.",
                    DateTimeOffset.Now))
                .ToArray();
            var request = new ChatRequest(Personality, _conversation, requestMessages, context);
            var visible = await RunProviderTurnAsync(provider, request, requestMessages, context);

            if (!string.IsNullOrWhiteSpace(visible))
            {
                AppendTranscript(Personality.DisplayName, visible, isUser: false);
            }
        }
        finally
        {
            SetBusy(false, "Ready");
        }
    }

    private async Task<string> RunProviderTurnAsync(
        IAiProvider provider,
        ChatRequest request,
        IReadOnlyList<ChatMessage> requestMessages,
        ChatContext context)
    {
        var raw = await StreamProviderResponseAsync(provider, request);
        var toolExtraction = AgentToolRequestParser.Extract(raw);
        var visible = toolExtraction.VisibleContent;

        if (toolExtraction.Request.HasCalls && _conversation is not null)
        {
            var toolMessage = await ExecuteToolRequestAsync(toolExtraction.Request);
            var followUpMessages = requestMessages.Append(toolMessage).ToArray();
            raw = await StreamProviderResponseAsync(provider, new ChatRequest(Personality, _conversation, followUpMessages, context));
            visible = ChatSelfManagementDirectiveParser.Extract(raw).VisibleContent;
        }

        var extraction = ChatSelfManagementDirectiveParser.Extract(raw);
        await ApplySelfManagementDirectiveAsync(extraction.Directive);
        return extraction.VisibleContent;
    }

    private async Task<string> StreamProviderResponseAsync(IAiProvider provider, ChatRequest request)
    {
        _responseCancellationTokenSource?.Dispose();
        _responseCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var raw = string.Empty;

        try
        {
            await foreach (var chunk in provider
                .StreamChatAsync(request, _responseCancellationTokenSource.Token)
                .WithCancellation(_responseCancellationTokenSource.Token))
            {
                raw += chunk.Delta;
                _status.Text = $"{Personality.DisplayName} is typing...";
                Application.DoEvents();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException or OperationCanceledException)
        {
            return await BuildProviderFailureMessageAsync(ex);
        }

        return raw;
    }

    private async Task<ChatMessage> ExecuteToolRequestAsync(AgentToolRequest request)
    {
        if (_conversation is null)
        {
            throw new InvalidOperationException("Cannot execute tools without an active conversation.");
        }

        var context = new AgentToolContext(Personality, _conversation);
        var toolDefinitions = _toolRegistry.ListTools().ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
        var results = new List<AgentToolResult>();

        foreach (var call in request.Calls.Take(4))
        {
            AgentToolResult result;

            if (toolDefinitions.TryGetValue(call.Name, out var definition) && definition.RequiresApproval)
            {
                AddPendingToolApproval(call);
                result = new AgentToolResult(call.Id, call.Name, false, "Pending user approval. No durable change has been made.");
            }
            else
            {
                result = await _toolRegistry.ExecuteAsync(call, context, _responseCancellationTokenSource?.Token ?? CancellationToken.None);
                await RefreshLocalStateAfterToolAsync(call);
            }

            results.Add(result);
            AppendToolTrace(call, result);
        }

        return new ChatMessage(
            Guid.NewGuid(),
            _conversation.Id,
            ChatRole.Tool,
            FormatToolResults(results),
            DateTimeOffset.Now);
    }

    private void AddPendingToolApproval(AgentToolCall call)
    {
        if (_conversation is null)
        {
            return;
        }

        var pendingAction = _pendingAgentActionQueue.CreateToolApproval(
            BuildToolApprovalTitle(call),
            BuildToolApprovalDetail(call),
            Personality.DisplayName,
            _conversation.Title,
            GetActionSourceKind(call.Name),
            Personality.Id,
            _conversation.Id,
            call);
        _pendingAgentActionQueue.Add(pendingAction);

        AddApproval(
            pendingAction.Title,
            pendingAction.Detail,
            async cancellationToken =>
            {
                if (_conversation is null)
                {
                    return null;
                }

                var result = await _toolRegistry.ExecuteAsync(
                    call,
                    new AgentToolContext(Personality, _conversation),
                    cancellationToken);
                await RefreshLocalStateAfterToolAsync(call);
                AppendToolTrace(call, result);
                _pendingAgentActionQueue.Remove(pendingAction.Id);

                return new ChatMessage(
                    Guid.NewGuid(),
                    _conversation.Id,
                    ChatRole.Tool,
                    FormatToolResults([result]),
                    DateTimeOffset.Now);
            },
            () => _pendingAgentActionQueue.Remove(pendingAction.Id));
    }

    private void AddApproval(
        string title,
        string detail,
        Func<CancellationToken, Task<ChatMessage?>> approveAsync,
        Action? onDeny = null)
    {
        var panel = new Panel
        {
            Width = Math.Max(360, ClientSize.Width - 28),
            Height = 58,
            BorderStyle = BorderStyle.Fixed3D,
            BackColor = Color.FromArgb(255, 250, 214),
            Margin = new Padding(0, 4, 0, 0)
        };
        var label = new Label
        {
            Text = $"{title}\r\n{detail}",
            Font = ClassicAim.SmallFont,
            Location = new Point(6, 5),
            Size = new Size(panel.Width - 160, 44),
            AutoEllipsis = true
        };
        var approve = ClassicAim.Button("Approve");
        approve.Size = new Size(82, 30);
        approve.Location = new Point(panel.Width - 164, 14);
        var deny = ClassicAim.Button("Deny");
        deny.Size = new Size(68, 30);
        deny.Location = new Point(panel.Width - 76, 14);

        approve.Click += async (_, _) =>
        {
            approve.Enabled = false;
            deny.Enabled = false;
            var toolMessage = await approveAsync(CancellationToken.None);
            _approvalPanel.Controls.Remove(panel);
            panel.Dispose();
            _approvalPanel.Visible = _approvalPanel.Controls.Count > 0;

            if (toolMessage is not null)
            {
                await ContinueAfterApprovedToolAsync(toolMessage);
            }
        };
        deny.Click += (_, _) =>
        {
            onDeny?.Invoke();
            _approvalPanel.Controls.Remove(panel);
            panel.Dispose();
            _approvalPanel.Visible = _approvalPanel.Controls.Count > 0;
        };

        panel.Controls.AddRange([label, approve, deny]);
        _approvalPanel.Controls.Add(panel);
        _approvalPanel.Visible = true;
    }

    private async Task ContinueAfterApprovedToolAsync(ChatMessage toolMessage)
    {
        if (_conversation is null || !TryResolveProvider(out var provider))
        {
            return;
        }

        SetBusy(true, $"{Personality.DisplayName} is typing...");

        try
        {
            var existingMessages = await _conversationService.GetMessagesAsync(_conversation.Id);
            var context = await _chatContextBuilder.BuildAsync(Personality, _conversation);
            var requestMessages = ChatRequestMessageWindow.Select(existingMessages, context).Append(toolMessage).ToArray();
            var visible = await RunProviderTurnAsync(provider, new ChatRequest(Personality, _conversation, requestMessages, context), requestMessages, context);

            if (!string.IsNullOrWhiteSpace(visible))
            {
                AppendTranscript(Personality.DisplayName, visible, isUser: false);
                await _conversationService.AddMessageAsync(_conversation.Id, ChatRole.Assistant, visible);
            }
        }
        finally
        {
            SetBusy(false, "Ready");
        }
    }

    private async Task ApplySelfManagementDirectiveAsync(ChatSelfManagementDirective directive)
    {
        if (!directive.HasChanges)
        {
            return;
        }

        foreach (var memory in directive.Memories)
        {
            AddApproval(
                memory.Action switch
                {
                    "remember" => "AI wants to remember",
                    "forget" => "AI wants to forget memory",
                    "update" => "AI wants to update memory",
                    _ => "AI wants to change memory"
                },
                memory.Action == "update" && !string.IsNullOrWhiteSpace(memory.OldContent)
                    ? $"{memory.OldContent} -> {memory.Content}"
                    : memory.Content,
                async cancellationToken =>
                {
                    await ApplyMemoryDirectiveAsync(memory, cancellationToken);
                    return null;
                });
        }

        if (directive.Personality is not null)
        {
            AddApproval(
                "AI wants to update personality",
                "Status or system prompt note",
                async cancellationToken =>
                {
                    await ApplyPersonalityDirectiveAsync(directive.Personality, cancellationToken);
                    return null;
                });
        }
    }

    private async Task ApplyMemoryDirectiveAsync(MemoryDirective directive, CancellationToken cancellationToken)
    {
        switch (directive.Action)
        {
            case "remember":
                await RememberIfMissingAsync(directive.Content, cancellationToken);
                break;
            case "forget":
                await ForgetMatchingMemoriesAsync(directive.Content, cancellationToken);
                break;
            case "update":
                await ForgetMatchingMemoriesAsync(string.IsNullOrWhiteSpace(directive.OldContent) ? directive.Content : directive.OldContent, cancellationToken);
                await RememberIfMissingAsync(directive.Content, cancellationToken);
                break;
        }
    }

    private async Task RememberIfMissingAsync(string content, CancellationToken cancellationToken)
    {
        var memories = await _memoryService.GetMemoriesAsync(Personality.Id, cancellationToken);

        if (!memories.Any(memory => string.Equals(memory.Content, content, StringComparison.OrdinalIgnoreCase)))
        {
            await _memoryService.RememberAsync(Personality.Id, content, cancellationToken);
        }
    }

    private async Task ForgetMatchingMemoriesAsync(string? content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var memories = await _memoryService.GetMemoriesAsync(Personality.Id, cancellationToken);
        var matches = memories
            .Where(memory =>
                string.Equals(memory.Content, content, StringComparison.OrdinalIgnoreCase) ||
                memory.Content.Contains(content, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var memory in matches)
        {
            await _memoryService.DeleteAsync(memory.Id, cancellationToken);
        }
    }

    private async Task ApplyPersonalityDirectiveAsync(PersonalityDirective directive, CancellationToken cancellationToken)
    {
        var status = string.IsNullOrWhiteSpace(directive.Status)
            ? Personality.Status
            : Truncate(directive.Status, 80);
        var systemPrompt = Personality.SystemPrompt;

        if (!string.IsNullOrWhiteSpace(directive.SystemPrompt))
        {
            systemPrompt = directive.SystemPrompt;
        }
        else if (!string.IsNullOrWhiteSpace(directive.SystemPromptAppend) &&
            !systemPrompt.Contains(directive.SystemPromptAppend, StringComparison.OrdinalIgnoreCase))
        {
            systemPrompt = $"{systemPrompt.Trim()}\n\nSelf-updated note: {directive.SystemPromptAppend.Trim()}";
        }

        var updated = await _personalityService.SaveAsync(new PersonalityDraft(
            Personality.Id,
            Personality.DisplayName,
            status,
            Personality.AvatarText,
            systemPrompt,
            Personality.ProviderKey,
            Personality.ModelId,
            Personality.AvatarImagePath,
            Personality.Category), cancellationToken);
        Personality = updated;
        Text = BuildWindowTitle(Personality);
    }

    private async Task RefreshLocalStateAfterToolAsync(AgentToolCall call)
    {
        if (call.Name.StartsWith("conversation.", StringComparison.OrdinalIgnoreCase))
        {
            await RefreshConversationHeaderAsync();
        }
        else if (call.Name.StartsWith("personality.", StringComparison.OrdinalIgnoreCase))
        {
            var personality = await _personalityService.GetAsync(Personality.Id);

            if (personality is not null)
            {
                Personality = personality;
            }
        }
    }

    private async Task RefreshConversationHeaderAsync()
    {
        if (_conversation is null)
        {
            return;
        }

        _conversation = await _conversationService.GetConversationAsync(_conversation.Id) ?? _conversation;
        _summary.Text = string.IsNullOrWhiteSpace(_conversation.Summary)
            ? "Summary: none"
            : $"Summary: {_conversation.Summary}";
    }

    private void AppendTranscript(string sender, string text, bool isUser)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _transcript.SelectionColor = isUser ? ClassicAim.TranscriptNameBlue : ClassicAim.TranscriptRemoteRed;
        _transcript.SelectionFont = ClassicAim.BoldFont;
        _transcript.AppendText($"{sender}: ");
        _transcript.SelectionColor = SystemColors.WindowText;
        _transcript.SelectionFont = _transcript.Font;
        _transcript.AppendText($"{text.Trim()}\r\n\r\n");
        _transcript.SelectionStart = _transcript.TextLength;
        _transcript.ScrollToCaret();
    }

    private void AppendToolTrace(AgentToolCall call, AgentToolResult result)
    {
        _status.Text = $"Tool {call.Name}: {(result.Success ? "ok" : "pending")}";
    }

    private static string FormatToolResults(IReadOnlyList<AgentToolResult> results)
    {
        var lines = results.Select(result =>
            $"- id={result.Id}; name={result.Name}; success={result.Success}; result={result.Content}");
        return string.Join('\n', lines);
    }

    private bool HasProviderSetupIssue
    {
        get
        {
            var diagnostic = _providerStatusService.GetCached(Personality.ProviderKey);
            return diagnostic is not null
                ? !diagnostic.IsUsable
                : _providerHealth is not null && !_providerHealth.IsReady;
        }
    }

    private async Task ShowProviderSetupAsync()
    {
        using var setup = new ProviderSetupForm(
            Personality.ProviderKey,
            _providerAccountService,
            _providerDiagnosticsService,
            _providerStatusService,
            _providers.ContainsKey(Personality.ProviderKey));

        if (setup.ShowDialog(this) == DialogResult.OK)
        {
            await RefreshProviderHealthAsync();
            AppendSetupNoticeIfNeeded();
        }
    }

    private async Task RefreshProviderHealthAsync(CancellationToken cancellationToken = default)
    {
        var account = await _providerAccountService.GetAsync(Personality.ProviderKey, cancellationToken);
        var diagnostic = _providerStatusService.GetCached(Personality.ProviderKey);
        _providerHealth = ProviderHealthEvaluator.Evaluate(
            Personality,
            account,
            _providers.ContainsKey(Personality.ProviderKey));

        var hasIssue = diagnostic is not null ? !diagnostic.IsUsable : HasProviderSetupIssue;
        _providerSetupPanel.Visible = hasIssue;
        _providerSetupLabel.Text = diagnostic?.Detail ?? _providerHealth.Detail;
        _status.Text = hasIssue ? diagnostic?.Label ?? _providerHealth.Label : "Ready";
    }

    private void AppendSetupNoticeIfNeeded()
    {
        var diagnostic = _providerStatusService.GetCached(Personality.ProviderKey);
        var hasIssue = diagnostic is not null ? !diagnostic.IsUsable : HasProviderSetupIssue;
        var detail = diagnostic?.Detail ?? _providerHealth?.Detail;

        if (!hasIssue || string.IsNullOrWhiteSpace(detail))
        {
            return;
        }

        AppendTranscript("AI-M", $"{Personality.DisplayName} cannot reply yet. {detail}", isUser: false);
    }

    private async Task<string> BuildProviderFailureMessageAsync(Exception exception)
    {
        if (exception is OperationCanceledException or TaskCanceledException)
        {
            return $"Provider '{Personality.ProviderKey}' request was canceled or timed out.";
        }

        try
        {
            var account = await _providerAccountService.GetAsync(Personality.ProviderKey);

            if (account is not null)
            {
                var diagnostic = await _providerDiagnosticsService.CheckAsync(
                    account,
                    _providers.ContainsKey(Personality.ProviderKey));

                if (!diagnostic.IsUsable || !diagnostic.IsVerified)
                {
                    return $"Provider '{Personality.ProviderKey}' could not complete the request. {diagnostic.Detail}";
                }
            }
        }
        catch (Exception diagnosticException) when (diagnosticException is HttpRequestException or InvalidOperationException or TaskCanceledException or OperationCanceledException)
        {
            return $"Provider '{Personality.ProviderKey}' could not complete the request. {exception.Message}";
        }

        return $"Provider '{Personality.ProviderKey}' could not complete the request. {exception.Message}";
    }

    private bool TryResolveProvider(out IAiProvider provider)
    {
        if (_providers.TryGetValue(Personality.ProviderKey, out var resolved))
        {
            provider = resolved;
            return true;
        }

        provider = null!;
        return false;
    }

    private void OnProviderStatusChanged(object? sender, ProviderStatusChangedEventArgs e)
    {
        if (!string.Equals(e.ProviderKey, Personality.ProviderKey, StringComparison.OrdinalIgnoreCase) || IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => _ = RefreshProviderHealthAsync()));
            return;
        }

        _ = RefreshProviderHealthAsync();
    }

    private static string BuildToolApprovalTitle(AgentToolCall call)
    {
        return call.Name switch
        {
            "memory.remember" => "AI wants to remember",
            "memory.forget" => "AI wants to forget memory",
            "personality.update_status" => "AI wants to update status",
            "personality.append_system_note" => "AI wants to update system prompt",
            "conversation.summary.update" => "AI wants to update conversation summary",
            _ => $"AI wants to run {call.Name}"
        };
    }

    private static string GetActionSourceKind(string toolName)
    {
        if (toolName.Contains("memory", StringComparison.OrdinalIgnoreCase))
        {
            return "Memory";
        }

        if (toolName.Contains("personality", StringComparison.OrdinalIgnoreCase))
        {
            return "Personality";
        }

        if (toolName.Contains("conversation", StringComparison.OrdinalIgnoreCase))
        {
            return "Conversation";
        }

        return "Tool";
    }

    private static string BuildToolApprovalDetail(AgentToolCall call)
    {
        return call.Name switch
        {
            "memory.remember" => GetArgumentString(call.Arguments, "content"),
            "memory.forget" => GetArgumentString(call.Arguments, "match"),
            "personality.update_status" => GetArgumentString(call.Arguments, "status"),
            "personality.append_system_note" => GetArgumentString(call.Arguments, "note"),
            "conversation.summary.update" => GetArgumentString(call.Arguments, "summary"),
            _ => call.Arguments.ToJsonString()
        };
    }

    private static string GetArgumentString(JsonObject arguments, string name)
    {
        return arguments[name]?.GetValue<string>()?.Trim() ?? string.Empty;
    }

    private void SetBusy(bool value, string status)
    {
        _isBusy = value;
        _input.Enabled = !value;
        _stopButton.Enabled = value;
        _status.Text = status;
    }

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].Trim();
    }
}
