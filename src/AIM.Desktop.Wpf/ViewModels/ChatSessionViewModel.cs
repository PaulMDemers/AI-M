using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json.Nodes;
using AIM.Core.Chat;
using AIM.Core.Personalities;
using AIM.Core.Providers;
using AIM.Core.Services;
using AIM.Core.Tools;
using AIM.Desktop.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class ChatSessionViewModel : ObservableObject
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
    private readonly IProviderSettingsWindowService _providerSettingsWindowService;
    private readonly PendingAgentActionService _pendingAgentActionService;
    private readonly IReadOnlyDictionary<string, IAiProvider> _providers;
    private Conversation? _conversation;
    private ConversationGroupViewModel? _selectedConversationGroup;
    private ConversationViewModel? _selectedConversation;
    private ProviderHealth? _providerHealth;
    private ProviderDiagnosticResult? _providerDiagnostic;
    private ChatMessageViewModel? _setupNoticeMessage;
    private string _conversationGroupTitle = string.Empty;
    private string _conversationTitle = string.Empty;
    private string _conversationSummary = string.Empty;
    private string _conversationSummaryUpdatedAtLabel = "No summary yet";
    private string _messageText = string.Empty;
    private CancellationTokenSource? _responseCancellationTokenSource;
    private bool _isBusy;

    public ChatSessionViewModel(
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
        IProviderSettingsWindowService providerSettingsWindowService,
        PendingAgentActionService pendingAgentActionService,
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
        _providerSettingsWindowService = providerSettingsWindowService;
        _pendingAgentActionService = pendingAgentActionService;
        _providers = providers.ToDictionary(provider => provider.Key, StringComparer.OrdinalIgnoreCase);
        SendMessageCommand = new AsyncRelayCommand(SendMessageAsync, CanSendMessage);
        CancelResponseCommand = new RelayCommand(CancelResponse, () => IsBusy);
        OpenProviderSettingsCommand = new AsyncRelayCommand(OpenProviderSettingsAsync);
        NewConversationGroupCommand = new AsyncRelayCommand(NewConversationGroupAsync);
        RenameConversationGroupCommand = new AsyncRelayCommand(RenameConversationGroupAsync, () => SelectedConversationGroup is not null && !string.IsNullOrWhiteSpace(ConversationGroupTitle));
        ArchiveConversationGroupCommand = new AsyncRelayCommand(ArchiveConversationGroupAsync, () => SelectedConversationGroup is not null);
        NewConversationCommand = new AsyncRelayCommand(NewConversationAsync);
        RenameConversationCommand = new AsyncRelayCommand(RenameConversationAsync, () => SelectedConversation is not null && !string.IsNullOrWhiteSpace(ConversationTitle));
        ArchiveConversationCommand = new AsyncRelayCommand(ArchiveConversationAsync, () => SelectedConversation is not null);
        SaveConversationSummaryCommand = new AsyncRelayCommand(SaveConversationSummaryAsync, () => SelectedConversation is not null);
        RefreshConversationSummaryCommand = new AsyncRelayCommand(RefreshConversationSummaryWithAiAsync, () => SelectedConversation is not null && !IsBusy && !HasProviderSetupIssue);
        ApprovePendingAgentActionCommand = new AsyncRelayCommand<PendingAgentActionViewModel>(ApprovePendingAgentActionAsync);
        DenyPendingAgentActionCommand = new RelayCommand<PendingAgentActionViewModel>(DenyPendingAgentAction);
        _providerStatusService.ProviderStatusChanged += OnProviderStatusChanged;
    }

    public Personality Personality { get; private set; }

    public string? AvatarImageUri => AvatarAssetResolver.Resolve(Personality.AvatarImagePath);

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public ObservableCollection<ToolTraceViewModel> ToolTraces { get; } = [];

    public ObservableCollection<PendingAgentActionViewModel> PendingAgentActions { get; } = [];

    public ObservableCollection<ConversationGroupViewModel> ConversationGroups { get; } = [];

    public ObservableCollection<ConversationViewModel> Conversations { get; } = [];

    public IAsyncRelayCommand SendMessageCommand { get; }

    public IRelayCommand CancelResponseCommand { get; }

    public IAsyncRelayCommand OpenProviderSettingsCommand { get; }

    public IAsyncRelayCommand NewConversationGroupCommand { get; }

    public IAsyncRelayCommand RenameConversationGroupCommand { get; }

    public IAsyncRelayCommand ArchiveConversationGroupCommand { get; }

    public IAsyncRelayCommand NewConversationCommand { get; }

    public IAsyncRelayCommand RenameConversationCommand { get; }

    public IAsyncRelayCommand ArchiveConversationCommand { get; }

    public IAsyncRelayCommand SaveConversationSummaryCommand { get; }

    public IAsyncRelayCommand RefreshConversationSummaryCommand { get; }

    public IAsyncRelayCommand<PendingAgentActionViewModel> ApprovePendingAgentActionCommand { get; }

    public IRelayCommand<PendingAgentActionViewModel> DenyPendingAgentActionCommand { get; }

    public ConversationGroupViewModel? SelectedConversationGroup
    {
        get => _selectedConversationGroup;
        set
        {
            if (SetProperty(ref _selectedConversationGroup, value))
            {
                ConversationGroupTitle = value?.Title ?? string.Empty;
                RenameConversationGroupCommand.NotifyCanExecuteChanged();
                ArchiveConversationGroupCommand.NotifyCanExecuteChanged();
                _ = SelectConversationGroupAsync(value);
            }
        }
    }

    public string ConversationGroupTitle
    {
        get => _conversationGroupTitle;
        set
        {
            if (SetProperty(ref _conversationGroupTitle, value))
            {
                RenameConversationGroupCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ConversationViewModel? SelectedConversation
    {
        get => _selectedConversation;
        set
        {
            if (SetProperty(ref _selectedConversation, value))
            {
                _ = SelectConversationAsync(value);
                ArchiveConversationCommand.NotifyCanExecuteChanged();
                RenameConversationCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public string ConversationTitle
    {
        get => _conversationTitle;
        set
        {
            if (SetProperty(ref _conversationTitle, value))
            {
                RenameConversationCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public string ConversationSummary
    {
        get => _conversationSummary;
        set => SetProperty(ref _conversationSummary, value);
    }

    public string ConversationSummaryUpdatedAtLabel
    {
        get => _conversationSummaryUpdatedAtLabel;
        private set => SetProperty(ref _conversationSummaryUpdatedAtLabel, value);
    }

    public string MessageText
    {
        get => _messageText;
        set
        {
            if (SetProperty(ref _messageText, value))
            {
                SendMessageCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(BusyText));
                SendMessageCommand.NotifyCanExecuteChanged();
                CancelResponseCommand.NotifyCanExecuteChanged();
                RefreshConversationSummaryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string BusyText => IsBusy ? $"{Personality.DisplayName} is thinking..." : string.Empty;

    public bool HasProviderSetupIssue =>
        _providerDiagnostic is not null
            ? !_providerDiagnostic.IsUsable
            : _providerHealth is not null && !_providerHealth.IsReady;

    public string ProviderSetupMessage => _providerDiagnostic?.Detail ?? _providerHealth?.Detail ?? string.Empty;

    public string ProviderSetupBrush => _providerDiagnostic is not null ? _providerDiagnostic.State switch
    {
        ProviderDiagnosticState.MissingProvider => "#DC2626",
        ProviderDiagnosticState.SetupNeeded => "#D97706",
        ProviderDiagnosticState.Unreachable => "#DC2626",
        ProviderDiagnosticState.Unauthorized => "#DC2626",
        ProviderDiagnosticState.Error => "#DC2626",
        ProviderDiagnosticState.Disabled => "#6B7280",
        _ => "#657085"
    } : _providerHealth?.State switch
    {
        ProviderHealthState.MissingProvider => "#DC2626",
        ProviderHealthState.NeedsSetup => "#D97706",
        ProviderHealthState.Disabled => "#6B7280",
        _ => "#657085"
    };

    public bool HasToolTraces => ToolTraces.Count > 0;

    public bool HasPendingAgentActions => PendingAgentActions.Count > 0;

    public string PendingAgentActionHeader => PendingAgentActions.Count == 1
        ? "Pending approval (1)"
        : $"Pending approvals ({PendingAgentActions.Count})";

    public string ToolTraceHeader => ToolTraces.Count == 1
        ? "Agent tools (1)"
        : $"Agent tools ({ToolTraces.Count})";

    public string WindowTitle => string.IsNullOrWhiteSpace(ConversationTitle)
        ? Personality.DisplayName
        : $"{Personality.DisplayName} - {ConversationTitle}";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RefreshProviderHealthAsync(cancellationToken);
        await ReloadConversationGroupsAsync(cancellationToken);

        if (SelectedConversationGroup is null)
        {
            var group = await _conversationService.GetOrCreateConversationGroupAsync(Personality.Id, cancellationToken);
            await ReloadConversationGroupsAsync(cancellationToken, selectedGroupId: group.Id);
        }

        if (SelectedConversation is null && SelectedConversationGroup is not null)
        {
            var conversation = await _conversationService.GetOrCreateConversationAsync(
                Personality.Id,
                SelectedConversationGroup.Id,
                cancellationToken);
            await ReloadConversationGroupsAsync(
                cancellationToken,
                selectedGroupId: SelectedConversationGroup.Id,
                selectedConversationId: conversation.Id);
        }
    }

    private async Task ReloadConversationGroupsAsync(
        CancellationToken cancellationToken = default,
        Guid? selectedGroupId = null,
        Guid? selectedConversationId = null)
    {
        selectedGroupId ??= SelectedConversationGroup?.Id;
        selectedConversationId ??= SelectedConversation?.Id;
        var groups = await _conversationService.ListConversationGroupsAsync(Personality.Id, cancellationToken);

        ConversationGroups.Clear();
        Conversations.Clear();

        foreach (var group in groups)
        {
            var groupViewModel = new ConversationGroupViewModel(group);
            var conversations = await _conversationService.ListConversationsAsync(
                Personality.Id,
                group.Id,
                cancellationToken);

            foreach (var conversation in conversations)
            {
                groupViewModel.Conversations.Add(new ConversationViewModel(conversation));
            }

            ConversationGroups.Add(groupViewModel);
        }

        var selectedGroup = ConversationGroups.FirstOrDefault(group => group.Id == selectedGroupId) ??
            ConversationGroups.FirstOrDefault();

        SelectedConversationGroup = selectedGroup;

        if (selectedGroup is not null)
        {
            ReplaceConversations(selectedGroup);
            SelectedConversation = Conversations.FirstOrDefault(conversation => conversation.Id == selectedConversationId) ??
                Conversations.FirstOrDefault();
        }
        else
        {
            SelectedConversation = null;
        }
    }

    private Task SelectConversationGroupAsync(ConversationGroupViewModel? groupViewModel)
    {
        ReplaceConversations(groupViewModel);
        SelectedConversation = Conversations.FirstOrDefault();
        return Task.CompletedTask;
    }

    private void ReplaceConversations(ConversationGroupViewModel? groupViewModel)
    {
        Conversations.Clear();

        if (groupViewModel is null)
        {
            return;
        }

        foreach (var conversation in groupViewModel.Conversations)
        {
            Conversations.Add(conversation);
        }
    }

    private async Task SelectConversationAsync(ConversationViewModel? conversationViewModel)
    {
        Messages.Clear();
        _setupNoticeMessage = null;
        ToolTraces.Clear();
        PendingAgentActions.Clear();
        OnToolTracesChanged();
        OnPendingAgentActionsChanged();
        _conversation = conversationViewModel?.Conversation;
        ConversationTitle = _conversation?.Title ?? string.Empty;
        ConversationSummary = _conversation?.Summary ?? string.Empty;
        ConversationSummaryUpdatedAtLabel = FormatSummaryUpdatedAt(_conversation?.SummaryUpdatedAt);
        SaveConversationSummaryCommand.NotifyCanExecuteChanged();
        RefreshConversationSummaryCommand.NotifyCanExecuteChanged();

        if (_conversation is null)
        {
            return;
        }

        var messages = await _conversationService.GetMessagesAsync(_conversation.Id);

        foreach (var message in messages)
        {
            Messages.Add(ChatMessageViewModel.FromMessage(message));
        }

        AddOrRemoveSetupNotice();
    }

    private async Task NewConversationAsync()
    {
        if (SelectedConversationGroup is null)
        {
            var group = await _conversationService.GetOrCreateConversationGroupAsync(Personality.Id);
            await ReloadConversationGroupsAsync(selectedGroupId: group.Id);
        }

        if (SelectedConversationGroup is null)
        {
            return;
        }

        var conversation = await _conversationService.CreateConversationAsync(
            Personality.Id,
            "New conversation",
            SelectedConversationGroup.Id);
        await ReloadConversationGroupsAsync(
            selectedGroupId: SelectedConversationGroup.Id,
            selectedConversationId: conversation.Id);
    }

    private async Task NewConversationGroupAsync()
    {
        var group = await _conversationService.CreateConversationGroupAsync(Personality.Id, "New group");
        var conversation = await _conversationService.CreateConversationAsync(
            Personality.Id,
            "New conversation",
            group.Id);
        await ReloadConversationGroupsAsync(
            selectedGroupId: group.Id,
            selectedConversationId: conversation.Id);
    }

    private async Task RenameConversationGroupAsync()
    {
        if (SelectedConversationGroup is null || string.IsNullOrWhiteSpace(ConversationGroupTitle))
        {
            return;
        }

        await _conversationService.RenameConversationGroupAsync(SelectedConversationGroup.Id, ConversationGroupTitle);
        await ReloadConversationGroupsAsync(selectedGroupId: SelectedConversationGroup.Id, selectedConversationId: SelectedConversation?.Id);
    }

    private async Task ArchiveConversationGroupAsync()
    {
        if (SelectedConversationGroup is null)
        {
            return;
        }

        await _conversationService.ArchiveConversationGroupAsync(SelectedConversationGroup.Id);
        await ReloadConversationGroupsAsync();
    }

    private async Task RenameConversationAsync()
    {
        if (_conversation is null || string.IsNullOrWhiteSpace(ConversationTitle))
        {
            return;
        }

        await _conversationService.RenameConversationAsync(_conversation.Id, ConversationTitle);
        await ReloadConversationGroupsAsync(
            selectedGroupId: _conversation.GroupId,
            selectedConversationId: _conversation.Id);
    }

    private async Task ArchiveConversationAsync()
    {
        if (_conversation is null)
        {
            return;
        }

        await _conversationService.ArchiveConversationAsync(_conversation.Id);
        await ReloadConversationGroupsAsync(selectedGroupId: _conversation.GroupId);
    }

    private async Task SaveConversationSummaryAsync()
    {
        if (_conversation is null)
        {
            return;
        }

        await _conversationService.UpdateConversationSummaryAsync(_conversation.Id, ConversationSummary);
        await RefreshConversationAsync();
    }

    private async Task RefreshConversationSummaryWithAiAsync()
    {
        if (_conversation is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        _responseCancellationTokenSource?.Dispose();
        _responseCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        try
        {
            await RequestQuietSummaryRefreshAsync();
        }
        finally
        {
            _responseCancellationTokenSource?.Dispose();
            _responseCancellationTokenSource = null;
            IsBusy = false;
        }
    }

    private bool CanSendMessage()
    {
        return !IsBusy && !HasProviderSetupIssue && !string.IsNullOrWhiteSpace(MessageText);
    }

    private async Task SendMessageAsync()
    {
        await SendMessageTextAsync(MessageText.Trim());
    }

    private async Task SendMessageTextAsync(string text)
    {
        if (_conversation is null)
        {
            await LoadAsync();
        }

        if (_conversation is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await RefreshProviderHealthAsync();

        if (HasProviderSetupIssue)
        {
            AddOrRemoveSetupNotice();
            return;
        }

        MessageText = string.Empty;
        IsBusy = true;
        _responseCancellationTokenSource?.Dispose();
        _responseCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        try
        {
            var userMessage = await _conversationService.AddMessageAsync(_conversation.Id, ChatRole.User, text);
            Messages.Add(ChatMessageViewModel.FromMessage(userMessage));

            if (!TryResolveProvider(out var provider))
            {
                var missingProviderMessage = $"Provider '{Personality.ProviderKey}' is not configured. " +
                    "For OpenAI, set OPENAI_API_KEY or AIM:Providers:OpenAI:ApiKey.";
                var storedMessage = await _conversationService.AddMessageAsync(
                    _conversation.Id,
                    ChatRole.Assistant,
                    missingProviderMessage);
                Messages.Add(ChatMessageViewModel.FromMessage(storedMessage));

                return;
            }

            var existingMessages = await _conversationService.GetMessagesAsync(_conversation.Id);
            var context = await _chatContextBuilder.BuildAsync(Personality, _conversation);
            var requestMessages = ChatRequestMessageWindow.Select(existingMessages, context);
            var request = new ChatRequest(Personality, _conversation, requestMessages, context);
            var assistantMessage = new ChatMessageViewModel(ChatRole.Assistant, string.Empty, DateTimeOffset.Now);
            Messages.Add(assistantMessage);
            await RunProviderTurnAsync(
                provider,
                request,
                requestMessages,
                context,
                assistantMessage,
                "Provider '{0}' request was canceled or timed out.",
                "Provider '{0}' could not complete the request. {1}",
                "Provider '{0}' request was canceled or timed out after tool use.",
                "Provider '{0}' could not complete the request after tool use. {1}");

            await _conversationService.AddMessageAsync(
                _conversation.Id,
                ChatRole.Assistant,
                assistantMessage.Content);
            await _memorySuggestionService.SuggestFromTurnAsync(
                Personality.Id,
                _conversation.Id,
                text,
                assistantMessage.Content);
        }
        finally
        {
            _responseCancellationTokenSource?.Dispose();
            _responseCancellationTokenSource = null;
            IsBusy = false;
        }
    }

    private void CancelResponse()
    {
        _responseCancellationTokenSource?.Cancel();
    }

    private async Task<string> StreamProviderResponseAsync(
        IAiProvider provider,
        ChatRequest request,
        ChatMessageViewModel assistantMessage)
    {
        if (_responseCancellationTokenSource is null)
        {
            return string.Empty;
        }

        var rawContent = string.Empty;

        await foreach (var chunk in provider
            .StreamChatAsync(request, _responseCancellationTokenSource.Token)
            .WithCancellation(_responseCancellationTokenSource.Token))
        {
            rawContent += chunk.Delta;
            assistantMessage.Content = HiddenMarkupDisplaySanitizer.Sanitize(rawContent);
        }

        return rawContent;
    }

    private async Task RunProviderTurnAsync(
        IAiProvider provider,
        ChatRequest request,
        IReadOnlyList<ChatMessage> requestMessages,
        ChatContext context,
        ChatMessageViewModel assistantMessage,
        string canceledMessageFormat,
        string failedMessageFormat,
        string toolCanceledMessageFormat,
        string toolFailedMessageFormat)
    {
        var rawAssistantContent = string.Empty;

        try
        {
            rawAssistantContent = await StreamProviderResponseAsync(provider, request, assistantMessage);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException or OperationCanceledException)
        {
            assistantMessage.Content = await BuildProviderFailureMessageAsync(
                ex,
                canceledMessageFormat,
                failedMessageFormat);
            rawAssistantContent = assistantMessage.Content;
        }

        var toolExtraction = AgentToolRequestParser.Extract(rawAssistantContent);
        assistantMessage.Content = toolExtraction.VisibleContent;

        if (toolExtraction.Request.HasCalls && _conversation is not null)
        {
            var toolMessage = await ExecuteToolRequestAsync(toolExtraction.Request);
            var followUpMessages = requestMessages
                .Append(toolMessage)
                .ToArray();
            var followUpRequest = new ChatRequest(Personality, _conversation, followUpMessages, context);
            assistantMessage.Content = string.Empty;
            rawAssistantContent = string.Empty;

            try
            {
                rawAssistantContent = await StreamProviderResponseAsync(provider, followUpRequest, assistantMessage);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException or OperationCanceledException)
            {
                assistantMessage.Content = await BuildProviderFailureMessageAsync(
                    ex,
                    toolCanceledMessageFormat,
                    toolFailedMessageFormat);
                rawAssistantContent = assistantMessage.Content;
            }
        }

        var extraction = ChatSelfManagementDirectiveParser.Extract(rawAssistantContent);
        assistantMessage.Content = extraction.VisibleContent;
        await ApplySelfManagementDirectiveAsync(extraction.Directive);
    }

    private async Task RequestQuietSummaryRefreshAsync()
    {
        if (_conversation is null)
        {
            return;
        }

        await RefreshProviderHealthAsync();

        if (HasProviderSetupIssue)
        {
            AddOrRemoveSetupNotice();
            return;
        }

        if (!TryResolveProvider(out var provider))
        {
            var storedMessage = await _conversationService.AddMessageAsync(
                _conversation.Id,
                ChatRole.Assistant,
                $"Summary refresh could not run because provider '{Personality.ProviderKey}' is not configured.");
            Messages.Add(ChatMessageViewModel.FromMessage(storedMessage));
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
                "Quiet app request: refresh the durable summary for this conversation. Keep it concise and capture stable goals, decisions, preferences, constraints, and open threads. Do not produce user-facing chat unless necessary. Prefer requesting conversation.summary.update if the summary should change.",
                DateTimeOffset.Now))
            .ToArray();
        var request = new ChatRequest(Personality, _conversation, requestMessages, context);
        var assistantMessage = new ChatMessageViewModel(ChatRole.Assistant, string.Empty, DateTimeOffset.Now);

        await RunProviderTurnAsync(
            provider,
            request,
            requestMessages,
            context,
            assistantMessage,
            "Provider '{0}' request was canceled or timed out during summary refresh.",
            "Provider '{0}' could not refresh the summary. {1}",
            "Provider '{0}' request was canceled or timed out after summary refresh tool use.",
            "Provider '{0}' could not complete summary refresh after tool use. {1}");

        if (!string.IsNullOrWhiteSpace(assistantMessage.Content))
        {
            Messages.Add(assistantMessage);
            await _conversationService.AddMessageAsync(
                _conversation.Id,
                ChatRole.Assistant,
                assistantMessage.Content);
        }
    }

    private async Task<ChatMessage> ExecuteToolRequestAsync(AgentToolRequest request)
    {
        if (_conversation is null)
        {
            throw new InvalidOperationException("Cannot execute tools without an active conversation.");
        }

        var context = new AgentToolContext(Personality, _conversation);
        var results = new List<AgentToolResult>();
        var toolDefinitions = _toolRegistry.ListTools().ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var call in request.Calls.Take(4))
        {
            AgentToolResult result;

            if (toolDefinitions.TryGetValue(call.Name, out var definition) && definition.RequiresApproval)
            {
                AddPendingToolApproval(call);
                result = new AgentToolResult(
                    call.Id,
                    call.Name,
                    false,
                    "Pending user approval. No durable change has been made.");
            }
            else
            {
                result = await _toolRegistry.ExecuteAsync(
                    call,
                    context,
                    _responseCancellationTokenSource?.Token ?? CancellationToken.None);
            }

            results.Add(result);
            AddToolTrace(call, result);
        }

        var content = FormatToolResults(results);
        return new ChatMessage(
            Guid.NewGuid(),
            _conversation.Id,
            ChatRole.Tool,
            content,
            DateTimeOffset.Now);
    }

    private static string FormatToolResults(IReadOnlyList<AgentToolResult> results)
    {
        var lines = results.Select(result =>
            $"- id={result.Id}; name={result.Name}; success={result.Success}; result={result.Content}");
        return string.Join('\n', lines);
    }

    private void AddPendingToolApproval(AgentToolCall call)
    {
        if (_conversation is null)
        {
            return;
        }

        var conversationId = _conversation.Id;
        AddPendingAgentAction(new PendingAgentActionViewModel(
            BuildToolApprovalTitle(call),
            BuildToolApprovalDetail(call),
            async cancellationToken =>
            {
                var result = await ExecuteApprovedToolCallAsync(call, conversationId, cancellationToken);
                if (_conversation is null || _conversation.Id != conversationId)
                {
                    return new PendingAgentActionResult(result.Content);
                }

                return new PendingAgentActionResult(
                    result.Content,
                    new ChatMessage(
                        Guid.NewGuid(),
                        _conversation.Id,
                        ChatRole.Tool,
                        FormatToolResults([result]),
                        DateTimeOffset.Now));
            },
            durableToolCall: new PendingAgentActionDurableToolCall(
                Personality.Id,
                conversationId,
                call)));
    }

    private async Task<AgentToolResult> ExecuteApprovedToolCallAsync(
        AgentToolCall call,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = _conversation is not null && _conversation.Id == conversationId
            ? _conversation
            : await _conversationService.GetConversationAsync(conversationId, cancellationToken);

        if (conversation is null)
        {
            return new AgentToolResult(call.Id, call.Name, false, "No active conversation.");
        }

        var result = await _toolRegistry.ExecuteAsync(
            call,
            new AgentToolContext(Personality, conversation),
            cancellationToken);

        if (call.Name.StartsWith("personality.", StringComparison.OrdinalIgnoreCase))
        {
            await RefreshPersonalityAsync(cancellationToken);
        }
        else if (call.Name.StartsWith("conversation.", StringComparison.OrdinalIgnoreCase))
        {
            if (_conversation?.Id == conversationId)
            {
                await RefreshConversationAsync(cancellationToken);
            }
        }

        AddToolTrace(call, result);
        return result;
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

    private void AddToolTrace(AgentToolCall call, AgentToolResult result)
    {
        ToolTraces.Add(ToolTraceViewModel.From(call, result));

        while (ToolTraces.Count > 50)
        {
            ToolTraces.RemoveAt(0);
        }

        OnToolTracesChanged();
    }

    private void AddPendingAgentAction(PendingAgentActionViewModel action)
    {
        action.AttachSource(
            Personality.DisplayName,
            ConversationTitle,
            GetActionSourceKind(action.Title));
        action.AttachHandlers(
            async () => await ApprovePendingAgentActionAsync(action),
            () => DenyPendingAgentAction(action));
        PendingAgentActions.Add(action);
        _pendingAgentActionService.Add(action);
        OnPendingAgentActionsChanged();
    }

    private async Task ApprovePendingAgentActionAsync(PendingAgentActionViewModel? action)
    {
        if (action is null || action.IsBusy || IsBusy)
        {
            return;
        }

        action.IsBusy = true;
        IsBusy = true;
        _responseCancellationTokenSource?.Dispose();
        _responseCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        try
        {
            var result = await action.ApproveAsync(_responseCancellationTokenSource.Token);
            PendingAgentActions.Remove(action);
            _pendingAgentActionService.Remove(action.Id);
            OnPendingAgentActionsChanged();

            if (result.ToolMessage is not null && _conversation?.Id == result.ToolMessage.ConversationId)
            {
                await ContinueAfterApprovedToolAsync(result.ToolMessage);
            }
        }
        finally
        {
            _responseCancellationTokenSource?.Dispose();
            _responseCancellationTokenSource = null;
            action.IsBusy = false;
            IsBusy = false;
        }
    }

    private async Task ContinueAfterApprovedToolAsync(ChatMessage approvedToolMessage)
    {
        if (_conversation is null)
        {
            return;
        }

        await RefreshProviderHealthAsync();

        if (HasProviderSetupIssue)
        {
            AddOrRemoveSetupNotice();
            return;
        }

        if (!TryResolveProvider(out var provider))
        {
            var storedMessage = await _conversationService.AddMessageAsync(
                _conversation.Id,
                ChatRole.Assistant,
                $"Approved action completed, but provider '{Personality.ProviderKey}' is not configured.");
            Messages.Add(ChatMessageViewModel.FromMessage(storedMessage));
            return;
        }

        var existingMessages = await _conversationService.GetMessagesAsync(_conversation.Id);
        var context = await _chatContextBuilder.BuildAsync(Personality, _conversation);
        var requestMessages = ChatRequestMessageWindow
            .Select(existingMessages, context)
            .Append(approvedToolMessage)
            .ToArray();
        var request = new ChatRequest(Personality, _conversation, requestMessages, context);
        var assistantMessage = new ChatMessageViewModel(ChatRole.Assistant, string.Empty, DateTimeOffset.Now);
        Messages.Add(assistantMessage);
        await RunProviderTurnAsync(
            provider,
            request,
            requestMessages,
            context,
            assistantMessage,
            "Provider '{0}' request was canceled or timed out after approval.",
            "Provider '{0}' could not continue after approval. {1}",
            "Provider '{0}' request was canceled or timed out after approved tool use.",
            "Provider '{0}' could not complete approved tool follow-up. {1}");

        await _conversationService.AddMessageAsync(
            _conversation.Id,
            ChatRole.Assistant,
            assistantMessage.Content);
    }

    private void DenyPendingAgentAction(PendingAgentActionViewModel? action)
    {
        if (action is null)
        {
            return;
        }

        PendingAgentActions.Remove(action);
        _pendingAgentActionService.Remove(action.Id);
        OnPendingAgentActionsChanged();
    }

    private static string GetActionSourceKind(string title)
    {
        if (title.Contains("remember", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("memory", StringComparison.OrdinalIgnoreCase))
        {
            return "Memory";
        }

        if (title.Contains("personality", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("system prompt", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("status", StringComparison.OrdinalIgnoreCase))
        {
            return "Personality";
        }

        if (title.Contains("summary", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("conversation", StringComparison.OrdinalIgnoreCase))
        {
            return "Conversation";
        }

        return "Tool";
    }

    private void OnToolTracesChanged()
    {
        OnPropertyChanged(nameof(HasToolTraces));
        OnPropertyChanged(nameof(ToolTraceHeader));
    }

    private void OnPendingAgentActionsChanged()
    {
        OnPropertyChanged(nameof(HasPendingAgentActions));
        OnPropertyChanged(nameof(PendingAgentActionHeader));
    }

    private async Task ApplySelfManagementDirectiveAsync(ChatSelfManagementDirective directive)
    {
        if (!directive.HasChanges)
        {
            return;
        }

        await ApplyMemoryDirectivesAsync(directive.Memories);
        await ApplyPersonalityDirectiveAsync(directive.Personality);
    }

    private async Task ApplyMemoryDirectivesAsync(IReadOnlyList<MemoryDirective> directives)
    {
        if (directives.Count == 0)
        {
            return;
        }

        foreach (var directive in directives)
        {
            AddPendingMemoryDirectiveApproval(directive);
        }

        await Task.CompletedTask;
    }

    private void AddPendingMemoryDirectiveApproval(MemoryDirective directive)
    {
        var title = directive.Action switch
        {
            "remember" => "AI wants to remember",
            "forget" => "AI wants to forget memory",
            "update" => "AI wants to update memory",
            _ => "AI wants to change memory"
        };
        var detail = directive.Action == "update" && !string.IsNullOrWhiteSpace(directive.OldContent)
            ? $"{directive.OldContent} -> {directive.Content}"
            : directive.Content;

        AddPendingAgentAction(new PendingAgentActionViewModel(
            title,
            detail,
            async cancellationToken =>
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
                        await ForgetMatchingMemoriesAsync(string.IsNullOrWhiteSpace(directive.OldContent)
                            ? directive.Content
                            : directive.OldContent, cancellationToken);
                        await RememberIfMissingAsync(directive.Content, cancellationToken);
                        break;
                }

                return new PendingAgentActionResult("Approved.");
            }));
    }

    private async Task RememberIfMissingAsync(string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var memories = await _memoryService.GetMemoriesAsync(Personality.Id, cancellationToken);

        if (memories.Any(memory => string.Equals(memory.Content, content, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        await _memoryService.RememberAsync(Personality.Id, content, cancellationToken);
    }

    private async Task ForgetMatchingMemoriesAsync(string? content, CancellationToken cancellationToken = default)
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

    private Task ApplyPersonalityDirectiveAsync(PersonalityDirective? directive)
    {
        if (directive is null)
        {
            return Task.CompletedTask;
        }

        AddPendingAgentAction(new PendingAgentActionViewModel(
            "AI wants to update personality",
            BuildPersonalityApprovalDetail(directive),
            async cancellationToken =>
            {
                await SavePersonalityDirectiveAsync(directive, cancellationToken);
                return new PendingAgentActionResult("Approved.");
            }));

        return Task.CompletedTask;
    }

    private async Task SavePersonalityDirectiveAsync(
        PersonalityDirective directive,
        CancellationToken cancellationToken = default)
    {
        var status = Truncate(string.IsNullOrWhiteSpace(directive.Status)
            ? Personality.Status
            : directive.Status, 80);
        var systemPrompt = BuildUpdatedSystemPrompt(directive);

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
        OnPropertyChanged(nameof(Personality));
        OnPropertyChanged(nameof(AvatarImageUri));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(BusyText));
    }

    private async Task RefreshPersonalityAsync(CancellationToken cancellationToken = default)
    {
        var updated = await _personalityService.GetAsync(Personality.Id, cancellationToken);

        if (updated is null)
        {
            return;
        }

        Personality = updated;
        OnPropertyChanged(nameof(Personality));
        OnPropertyChanged(nameof(AvatarImageUri));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(BusyText));
        await RefreshProviderHealthAsync(cancellationToken);
    }

    private async Task RefreshConversationAsync(CancellationToken cancellationToken = default)
    {
        if (_conversation is null)
        {
            return;
        }

        var updated = await _conversationService.GetConversationAsync(_conversation.Id, cancellationToken);

        if (updated is null)
        {
            return;
        }

        _conversation = updated;
        ConversationSummary = updated.Summary;
        ConversationSummaryUpdatedAtLabel = FormatSummaryUpdatedAt(updated.SummaryUpdatedAt);
        OnPropertyChanged(nameof(WindowTitle));
    }

    private static string FormatSummaryUpdatedAt(DateTimeOffset? value)
    {
        return value is null ? "No summary yet" : $"Updated {value:g}";
    }

    private static string BuildPersonalityApprovalDetail(PersonalityDirective directive)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(directive.Status))
        {
            parts.Add($"Status: {directive.Status}");
        }

        if (!string.IsNullOrWhiteSpace(directive.SystemPrompt))
        {
            parts.Add("Replace system prompt.");
        }

        if (!string.IsNullOrWhiteSpace(directive.SystemPromptAppend))
        {
            parts.Add($"Append system note: {directive.SystemPromptAppend}");
        }

        return parts.Count == 0 ? "No visible personality changes." : string.Join("\n", parts);
    }

    private string BuildUpdatedSystemPrompt(PersonalityDirective directive)
    {
        if (!string.IsNullOrWhiteSpace(directive.SystemPrompt))
        {
            return directive.SystemPrompt;
        }

        if (string.IsNullOrWhiteSpace(directive.SystemPromptAppend))
        {
            return Personality.SystemPrompt;
        }

        if (Personality.SystemPrompt.Contains(directive.SystemPromptAppend, StringComparison.OrdinalIgnoreCase))
        {
            return Personality.SystemPrompt;
        }

        return $"{Personality.SystemPrompt.Trim()}\n\nSelf-updated note: {directive.SystemPromptAppend.Trim()}";
    }

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].Trim();
    }

    private async Task OpenProviderSettingsAsync()
    {
        await _providerSettingsWindowService.OpenAsync(Personality.ProviderKey);
        _providerStatusService.Clear(Personality.ProviderKey);
        await RefreshProviderHealthAsync();
    }

    private async Task RefreshProviderHealthAsync(CancellationToken cancellationToken = default)
    {
        var account = await _providerAccountService.GetAsync(Personality.ProviderKey, cancellationToken);
        var health = ProviderHealthEvaluator.Evaluate(
            Personality,
            account,
            _providers.ContainsKey(Personality.ProviderKey));
        var diagnostic = _providerStatusService.GetCached(Personality.ProviderKey);

        if (!Equals(_providerHealth, health) || !Equals(_providerDiagnostic, diagnostic))
        {
            _providerHealth = health;
            _providerDiagnostic = diagnostic;
            OnPropertyChanged(nameof(HasProviderSetupIssue));
            OnPropertyChanged(nameof(ProviderSetupMessage));
            OnPropertyChanged(nameof(ProviderSetupBrush));
            SendMessageCommand.NotifyCanExecuteChanged();
            RefreshConversationSummaryCommand.NotifyCanExecuteChanged();
        }

        AddOrRemoveSetupNotice();
    }

    private async Task<string> BuildProviderFailureMessageAsync(
        Exception exception,
        string canceledMessageFormat,
        string failedMessageFormat)
    {
        if (exception is OperationCanceledException or TaskCanceledException)
        {
            return string.Format(canceledMessageFormat, Personality.ProviderKey);
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
            return string.Format(failedMessageFormat, Personality.ProviderKey, exception.Message);
        }

        return string.Format(failedMessageFormat, Personality.ProviderKey, exception.Message);
    }

    private void AddOrRemoveSetupNotice()
    {
        if (HasProviderSetupIssue)
        {
            var content = BuildSetupNoticeText();

            if (_setupNoticeMessage is null)
            {
                _setupNoticeMessage = new ChatMessageViewModel(ChatRole.System, content, DateTimeOffset.Now);
                Messages.Add(_setupNoticeMessage);
            }
            else
            {
                _setupNoticeMessage.Content = content;
            }

            return;
        }

        if (_setupNoticeMessage is not null)
        {
            Messages.Remove(_setupNoticeMessage);
            _setupNoticeMessage = null;
        }
    }

    private string BuildSetupNoticeText()
    {
        var detail = _providerDiagnostic?.Detail ?? _providerHealth?.Detail;

        if (string.IsNullOrWhiteSpace(detail))
        {
            return string.Empty;
        }

        return $"{Personality.DisplayName} cannot reply yet. {detail}";
    }

    private void OnProviderStatusChanged(object? sender, ProviderStatusChangedEventArgs e)
    {
        if (string.Equals(e.ProviderKey, Personality.ProviderKey, StringComparison.OrdinalIgnoreCase))
        {
            _ = RefreshProviderHealthAsync();
        }
    }

    private bool TryResolveProvider(out IAiProvider provider)
    {
        if (_providers.TryGetValue(Personality.ProviderKey, out var resolvedProvider))
        {
            provider = resolvedProvider;
            return true;
        }

        provider = null!;
        return false;
    }
}
