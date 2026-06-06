using System.Collections.ObjectModel;
using AIM.Core.PendingActions;
using AIM.Core.Services;
using AIM.Core.Tools;
using AIM.Desktop.Wpf.ViewModels;

namespace AIM.Desktop.Wpf.Services;

public sealed class PendingAgentActionService
{
    private readonly IPendingAgentActionQueue _pendingAgentActionQueue;

    public PendingAgentActionService(IPendingAgentActionQueue pendingAgentActionQueue)
    {
        _pendingAgentActionQueue = pendingAgentActionQueue;

        foreach (var action in _pendingAgentActionQueue.Actions)
        {
            Actions.Add(CreateRestoredViewModel(action));
        }
    }

    public PendingAgentActionService(string? storagePath = null)
        : this(new FilePendingAgentActionQueue(storagePath))
    {
    }

    public PendingAgentActionService(
        string? storagePath,
        IPersonalityService personalityService,
        IConversationService conversationService,
        IAgentToolRegistry toolRegistry)
        : this(new FilePendingAgentActionQueue(
            storagePath,
            personalityService,
            conversationService,
            toolRegistry))
    {
    }

    public ObservableCollection<PendingAgentActionViewModel> Actions { get; } = [];

    public event EventHandler? ActionsChanged;

    public void Add(PendingAgentActionViewModel action)
    {
        if (Actions.Any(existing => existing.Id == action.Id))
        {
            return;
        }

        Actions.Add(action);
        _pendingAgentActionQueue.Add(ToPendingAction(action));
        ActionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(Guid actionId)
    {
        var action = Actions.FirstOrDefault(candidate => candidate.Id == actionId);

        if (action is not null)
        {
            Actions.Remove(action);
        }

        _pendingAgentActionQueue.Remove(actionId);
        ActionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private PendingAgentActionViewModel CreateRestoredViewModel(PendingAgentAction action)
    {
        var viewModel = new PendingAgentActionViewModel(
            action.Title,
            action.Detail,
            cancellationToken => _pendingAgentActionQueue.ApproveAsync(action, cancellationToken),
            action.Id,
            action.CreatedAt,
            action.CanApprove,
            action.ApprovalNote,
            action.ToolCall);
        viewModel.AttachSource(action.SourcePersonality, action.SourceConversation, action.SourceKind);
        viewModel.AttachHandlers(
            async () => await ApproveRestoredActionAsync(viewModel, action),
            () => Remove(viewModel.Id));
        return viewModel;
    }

    private async Task ApproveRestoredActionAsync(
        PendingAgentActionViewModel viewModel,
        PendingAgentAction action)
    {
        if (viewModel.IsBusy || !viewModel.CanApprove)
        {
            return;
        }

        viewModel.IsBusy = true;

        try
        {
            await _pendingAgentActionQueue.ApproveAsync(action);
            var local = Actions.FirstOrDefault(candidate => candidate.Id == viewModel.Id);
            if (local is not null)
            {
                Actions.Remove(local);
                ActionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            viewModel.IsBusy = false;
        }
    }

    private static PendingAgentAction ToPendingAction(PendingAgentActionViewModel action)
    {
        return new PendingAgentAction(
            action.Id,
            action.Title,
            action.Detail,
            action.CreatedAt,
            action.SourcePersonality,
            action.SourceConversation,
            action.SourceKind,
            action.DurableToolCall);
    }
}
