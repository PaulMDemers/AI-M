using System.Collections.ObjectModel;
using AIM.Desktop.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class PendingActionsReviewViewModel : ObservableObject
{
    private readonly PendingAgentActionService _pendingAgentActionService;

    public PendingActionsReviewViewModel(PendingAgentActionService pendingAgentActionService)
    {
        _pendingAgentActionService = pendingAgentActionService;
        ApproveCommand = new AsyncRelayCommand<PendingAgentActionViewModel>(ApproveAsync);
        DenyCommand = new RelayCommand<PendingAgentActionViewModel>(Deny);
        _pendingAgentActionService.ActionsChanged += (_, _) => Refresh();
    }

    public ObservableCollection<PendingAgentActionViewModel> Actions => _pendingAgentActionService.Actions;

    public IAsyncRelayCommand<PendingAgentActionViewModel> ApproveCommand { get; }

    public IRelayCommand<PendingAgentActionViewModel> DenyCommand { get; }

    public bool HasActions => Actions.Count > 0;

    public string Header => Actions.Count == 1
        ? "1 pending AI action"
        : $"{Actions.Count} pending AI actions";

    public void Refresh()
    {
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(Header));
    }

    private async Task ApproveAsync(PendingAgentActionViewModel? action)
    {
        if (action is null || action.IsBusy || !action.CanApprove)
        {
            return;
        }

        await action.ApproveFromAnyViewAsync();
        Refresh();
    }

    private void Deny(PendingAgentActionViewModel? action)
    {
        action?.DenyFromAnyView();
        Refresh();
    }
}
