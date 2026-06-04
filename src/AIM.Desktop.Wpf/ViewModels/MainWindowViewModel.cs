using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using AIM.Core.Providers;
using AIM.Core.Services;
using AIM.Desktop.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IPersonalityService _personalityService;
    private readonly IProviderAccountService _providerAccountService;
    private readonly IProviderStatusService _providerStatusService;
    private readonly IReadOnlySet<string> _registeredProviderKeys;
    private readonly ChatSessionViewModelFactory _chatSessionFactory;
    private readonly IChatWindowService _chatWindowService;
    private readonly IProviderSettingsWindowService _providerSettingsWindowService;
    private readonly IPersonalityEditorWindowService _personalityEditorWindowService;
    private readonly IMemoryReviewWindowService _memoryReviewWindowService;
    private readonly PendingAgentActionService _pendingAgentActionService;
    private readonly IPendingActionsReviewWindowService _pendingActionsReviewWindowService;
    private FriendViewModel? _selectedFriend;
    private ChatSessionViewModel? _currentChat;
    private bool _isAllInOneMode;
    private bool _isCheckingProviders;
    private string _providerSummary = "Provider checks not run";

    public MainWindowViewModel(
        IPersonalityService personalityService,
        IProviderAccountService providerAccountService,
        IProviderStatusService providerStatusService,
        IEnumerable<IAiProvider> providers,
        ChatSessionViewModelFactory chatSessionFactory,
        IChatWindowService chatWindowService,
        IProviderSettingsWindowService providerSettingsWindowService,
        IPersonalityEditorWindowService personalityEditorWindowService,
        IMemoryReviewWindowService memoryReviewWindowService,
        PendingAgentActionService pendingAgentActionService,
        IPendingActionsReviewWindowService pendingActionsReviewWindowService)
    {
        _personalityService = personalityService;
        _providerAccountService = providerAccountService;
        _providerStatusService = providerStatusService;
        _registeredProviderKeys = providers
            .Select(provider => provider.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _chatSessionFactory = chatSessionFactory;
        _chatWindowService = chatWindowService;
        _providerSettingsWindowService = providerSettingsWindowService;
        _personalityEditorWindowService = personalityEditorWindowService;
        _memoryReviewWindowService = memoryReviewWindowService;
        _pendingAgentActionService = pendingAgentActionService;
        _pendingActionsReviewWindowService = pendingActionsReviewWindowService;
        OpenFloatingChatCommand = new AsyncRelayCommand(OpenFloatingChatAsync, () => SelectedFriend is not null);
        OpenProviderSettingsCommand = new AsyncRelayCommand(OpenProviderSettingsAsync);
        CheckProvidersCommand = new AsyncRelayCommand(CheckProvidersAsync, () => !IsCheckingProviders);
        OpenPendingActionsCommand = new AsyncRelayCommand(OpenPendingActionsAsync);
        AddPersonalityCommand = new AsyncRelayCommand(AddPersonalityAsync);
        EditPersonalityCommand = new AsyncRelayCommand(EditPersonalityAsync, () => SelectedFriend is not null);
        ReviewMemoryCommand = new AsyncRelayCommand(ReviewMemoryAsync, () => SelectedFriend is not null);
        ToggleAllInOneModeCommand = new RelayCommand(ToggleAllInOneMode);
        FriendsView = CollectionViewSource.GetDefaultView(Friends);
        FriendsView.GroupDescriptions?.Add(new PropertyGroupDescription(nameof(FriendViewModel.Category)));
        FriendsView.SortDescriptions.Add(new SortDescription(nameof(FriendViewModel.CategorySortOrder), ListSortDirection.Ascending));
        FriendsView.SortDescriptions.Add(new SortDescription(nameof(FriendViewModel.DisplayName), ListSortDirection.Ascending));
        _providerStatusService.ProviderStatusChanged += OnProviderStatusChanged;
        _pendingAgentActionService.ActionsChanged += OnPendingActionsChanged;

        _ = LoadFriendsAsync();
    }

    public ObservableCollection<FriendViewModel> Friends { get; } = [];

    public ICollectionView FriendsView { get; }

    public IAsyncRelayCommand OpenFloatingChatCommand { get; }

    public IAsyncRelayCommand OpenProviderSettingsCommand { get; }

    public IAsyncRelayCommand CheckProvidersCommand { get; }

    public IAsyncRelayCommand OpenPendingActionsCommand { get; }

    public IAsyncRelayCommand AddPersonalityCommand { get; }

    public IAsyncRelayCommand EditPersonalityCommand { get; }

    public IAsyncRelayCommand ReviewMemoryCommand { get; }

    public IRelayCommand ToggleAllInOneModeCommand { get; }

    public string ProviderSummary
    {
        get => _providerSummary;
        private set => SetProperty(ref _providerSummary, value);
    }

    public bool IsCheckingProviders
    {
        get => _isCheckingProviders;
        private set
        {
            if (SetProperty(ref _isCheckingProviders, value))
            {
                CheckProvidersCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int PendingActionCount => _pendingAgentActionService.Actions.Count;

    public bool HasPendingActions => PendingActionCount > 0;

    public string PendingActionsStatusText => PendingActionCount == 1
        ? "1 AI action needs review"
        : $"{PendingActionCount} AI actions need review";

    public string PendingActionsMenuText => PendingActionCount == 0
        ? "Pending AI Actions"
        : $"Pending AI Actions ({PendingActionCount})";

    public bool IsAllInOneMode
    {
        get => _isAllInOneMode;
        private set
        {
            if (SetProperty(ref _isAllInOneMode, value))
            {
                OnPropertyChanged(nameof(ShellModeButtonText));
                OnPropertyChanged(nameof(ShellWidth));
                OnPropertyChanged(nameof(ShellMinWidth));

                if (value)
                {
                    _ = LoadSelectedFriendAsync();
                }
                else
                {
                    CurrentChat = null;
                }
            }
        }
    }

    public string ShellModeButtonText => IsAllInOneMode ? "Contacts" : "All in One";

    public double ShellWidth => IsAllInOneMode ? 1080 : 280;

    public double ShellMinWidth => IsAllInOneMode ? 900 : 260;

    public FriendViewModel? SelectedFriend
    {
        get => _selectedFriend;
        set
        {
            if (SetProperty(ref _selectedFriend, value))
            {
                OpenFloatingChatCommand.NotifyCanExecuteChanged();
                EditPersonalityCommand.NotifyCanExecuteChanged();
                ReviewMemoryCommand.NotifyCanExecuteChanged();

                if (IsAllInOneMode)
                {
                    _ = LoadSelectedFriendAsync();
                }
                else
                {
                    CurrentChat = null;
                }
            }
        }
    }

    public ChatSessionViewModel? CurrentChat
    {
        get => _currentChat;
        private set => SetProperty(ref _currentChat, value);
    }

    public Task RefreshFriendsAsync()
    {
        return LoadFriendsAsync();
    }

    private async Task LoadFriendsAsync()
    {
        var selectedId = SelectedFriend?.Personality.Id;
        var personalities = await _personalityService.ListAsync();
        var providerAccounts = (await _providerAccountService.ListAsync())
            .ToDictionary(account => account.Key, StringComparer.OrdinalIgnoreCase);

        Friends.Clear();

        foreach (var personality in personalities)
        {
            providerAccounts.TryGetValue(personality.ProviderKey, out var account);
            var health = ProviderHealthEvaluator.Evaluate(
                personality,
                account,
                _registeredProviderKeys.Contains(personality.ProviderKey));
            var diagnostic = _providerStatusService.GetCached(personality.ProviderKey);
            Friends.Add(new FriendViewModel(personality, health, diagnostic));
        }

        SelectedFriend = Friends.FirstOrDefault(friend => friend.Personality.Id == selectedId) ?? Friends.FirstOrDefault();
    }

    private async Task LoadSelectedFriendAsync()
    {
        if (SelectedFriend is null)
        {
            CurrentChat = null;
            return;
        }

        var chat = _chatSessionFactory.Create(SelectedFriend.Personality);
        CurrentChat = chat;
        await chat.LoadAsync();
    }

    private async Task OpenFloatingChatAsync()
    {
        if (SelectedFriend is not null)
        {
            await _chatWindowService.OpenFloatingChatAsync(SelectedFriend.Personality);
        }
    }

    private void ToggleAllInOneMode()
    {
        IsAllInOneMode = !IsAllInOneMode;
    }

    private async Task OpenProviderSettingsAsync()
    {
        var providerKey = SelectedFriend?.Personality.ProviderKey;
        await _providerSettingsWindowService.OpenAsync(providerKey);
        InvalidateProviderDiagnostic(providerKey);
        await LoadFriendsAsync();
    }

    private void InvalidateProviderDiagnostic(string? providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey) || _providerStatusService.GetCached(providerKey) is null)
        {
            return;
        }

        _providerStatusService.Clear(providerKey);
        var snapshot = _providerStatusService.Snapshot();
        ProviderSummary = snapshot.Count == 0
            ? "Provider checks not run"
            : BuildProviderSummary(snapshot.Values);
    }

    private async Task CheckProvidersAsync()
    {
        IsCheckingProviders = true;
        ProviderSummary = "Checking providers...";

        try
        {
            var diagnostics = await _providerStatusService.RefreshAllAsync();
            ProviderSummary = BuildProviderSummary(diagnostics.Values);
            await LoadFriendsAsync();
        }
        finally
        {
            IsCheckingProviders = false;
        }
    }

    private Task OpenPendingActionsAsync()
    {
        return _pendingActionsReviewWindowService.OpenAsync();
    }

    private void OnProviderStatusChanged(object? sender, ProviderStatusChangedEventArgs e)
    {
        var snapshot = _providerStatusService.Snapshot();
        ProviderSummary = snapshot.Count == 0
            ? "Provider checks not run"
            : BuildProviderSummary(snapshot.Values);
        _ = LoadFriendsAsync();
    }

    private void OnPendingActionsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PendingActionCount));
        OnPropertyChanged(nameof(HasPendingActions));
        OnPropertyChanged(nameof(PendingActionsStatusText));
        OnPropertyChanged(nameof(PendingActionsMenuText));
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

    private async Task AddPersonalityAsync()
    {
        if (await _personalityEditorWindowService.OpenAsync(null))
        {
            await LoadFriendsAsync();
        }
    }

    private async Task EditPersonalityAsync()
    {
        if (SelectedFriend is not null &&
            await _personalityEditorWindowService.OpenAsync(SelectedFriend.Personality))
        {
            await LoadFriendsAsync();
        }
    }

    private async Task ReviewMemoryAsync()
    {
        if (SelectedFriend is not null)
        {
            await _memoryReviewWindowService.OpenAsync(SelectedFriend.Personality);
        }
    }
}
