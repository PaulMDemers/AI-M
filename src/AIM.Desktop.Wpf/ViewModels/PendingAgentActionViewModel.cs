using AIM.Core.Chat;
using AIM.Core.PendingActions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class PendingAgentActionViewModel : ObservableObject
{
    private bool _isBusy;
    private string _sourcePersonality = string.Empty;
    private string _sourceConversation = string.Empty;
    private string _sourceKind = "AI action";
    private Func<Task>? _requestApproveAsync;
    private Action? _requestDeny;

    public PendingAgentActionViewModel(
        string title,
        string detail,
        Func<CancellationToken, Task<PendingAgentActionResult>> approveAsync,
        Guid? id = null,
        DateTimeOffset? createdAt = null,
        bool canApprove = true,
        string approvalUnavailableText = "",
        PendingAgentActionDurableToolCall? durableToolCall = null)
    {
        Id = id ?? Guid.NewGuid();
        Title = title;
        Detail = detail;
        CreatedAt = createdAt ?? DateTimeOffset.Now;
        ApproveAsync = approveAsync;
        CanApprove = canApprove;
        ApprovalUnavailableText = approvalUnavailableText;
        DurableToolCall = durableToolCall;
    }

    public Guid Id { get; }

    public string Title { get; }

    public string Detail { get; }

    public DateTimeOffset CreatedAt { get; }

    public string CreatedAtLabel => CreatedAt.ToString("t");

    public bool CanApprove { get; }

    public string ApprovalUnavailableText { get; }

    public bool IsApprovalUnavailable => !CanApprove && !string.IsNullOrWhiteSpace(ApprovalUnavailableText);

    public bool HasApprovalNote => !string.IsNullOrWhiteSpace(ApprovalUnavailableText);

    public PendingAgentActionDurableToolCall? DurableToolCall { get; }

    public string SourcePersonality
    {
        get => _sourcePersonality;
        private set => SetProperty(ref _sourcePersonality, value);
    }

    public string SourceConversation
    {
        get => _sourceConversation;
        private set => SetProperty(ref _sourceConversation, value);
    }

    public string SourceKind
    {
        get => _sourceKind;
        private set => SetProperty(ref _sourceKind, value);
    }

    public string SourceLabel
    {
        get
        {
            var source = string.IsNullOrWhiteSpace(SourceConversation)
                ? SourcePersonality
                : $"{SourcePersonality} / {SourceConversation}";
            return string.IsNullOrWhiteSpace(source) ? SourceKind : $"{SourceKind} from {source}";
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    internal Func<CancellationToken, Task<PendingAgentActionResult>> ApproveAsync { get; }

    public void AttachSource(string personalityName, string conversationTitle, string sourceKind)
    {
        SourcePersonality = personalityName;
        SourceConversation = conversationTitle;
        SourceKind = sourceKind;
        OnPropertyChanged(nameof(SourceLabel));
    }

    public void AttachHandlers(Func<Task> requestApproveAsync, Action requestDeny)
    {
        _requestApproveAsync = requestApproveAsync;
        _requestDeny = requestDeny;
    }

    public Task ApproveFromAnyViewAsync()
    {
        if (!CanApprove)
        {
            return Task.CompletedTask;
        }

        return _requestApproveAsync is not null
            ? _requestApproveAsync()
            : ApproveAsync(CancellationToken.None);
    }

    public void DenyFromAnyView()
    {
        if (_requestDeny is not null)
        {
            _requestDeny();
        }
    }
}
