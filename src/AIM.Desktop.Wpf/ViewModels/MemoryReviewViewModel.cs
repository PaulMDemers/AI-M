using System.Collections.ObjectModel;
using AIM.Core.Personalities;
using AIM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class MemoryReviewViewModel : ObservableObject
{
    private readonly IMemorySuggestionService _memorySuggestionService;
    private Personality? _personality;
    private MemorySuggestionViewModel? _selectedSuggestion;

    public MemoryReviewViewModel(IMemorySuggestionService memorySuggestionService)
    {
        _memorySuggestionService = memorySuggestionService;
        ApproveCommand = new AsyncRelayCommand(ApproveAsync, () => SelectedSuggestion is not null);
        RejectCommand = new AsyncRelayCommand(RejectAsync, () => SelectedSuggestion is not null);
    }

    public ObservableCollection<MemorySuggestionViewModel> Suggestions { get; } = [];

    public IAsyncRelayCommand ApproveCommand { get; }

    public IAsyncRelayCommand RejectCommand { get; }

    public string Title => _personality is null ? "Memory Review" : $"Memory Review - {_personality.DisplayName}";

    public MemorySuggestionViewModel? SelectedSuggestion
    {
        get => _selectedSuggestion;
        set
        {
            if (SetProperty(ref _selectedSuggestion, value))
            {
                ApproveCommand.NotifyCanExecuteChanged();
                RejectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public async Task LoadAsync(Personality personality, CancellationToken cancellationToken = default)
    {
        _personality = personality;
        OnPropertyChanged(nameof(Title));
        await RefreshAsync(cancellationToken);
    }

    private async Task ApproveAsync()
    {
        if (SelectedSuggestion is null)
        {
            return;
        }

        await _memorySuggestionService.ApproveAsync(SelectedSuggestion.Id);
        await RefreshAsync();
    }

    private async Task RejectAsync()
    {
        if (SelectedSuggestion is null)
        {
            return;
        }

        await _memorySuggestionService.RejectAsync(SelectedSuggestion.Id);
        await RefreshAsync();
    }

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Suggestions.Clear();
        SelectedSuggestion = null;

        if (_personality is null)
        {
            return;
        }

        var suggestions = await _memorySuggestionService.ListPendingAsync(_personality.Id, cancellationToken);

        foreach (var suggestion in suggestions)
        {
            Suggestions.Add(new MemorySuggestionViewModel(suggestion));
        }
    }
}
