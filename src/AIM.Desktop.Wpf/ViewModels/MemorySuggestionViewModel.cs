using AIM.Core.Memory;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class MemorySuggestionViewModel
{
    public MemorySuggestionViewModel(MemorySuggestion suggestion)
    {
        Suggestion = suggestion;
    }

    public MemorySuggestion Suggestion { get; }

    public Guid Id => Suggestion.Id;

    public string Content => Suggestion.Content;

    public string CreatedAt => Suggestion.CreatedAt.ToString("g");
}
