using System.Collections.ObjectModel;
using AIM.Core.Chat;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class ConversationGroupViewModel
{
    public ConversationGroupViewModel(ConversationGroup group)
    {
        Group = group;
    }

    public ConversationGroup Group { get; }

    public Guid Id => Group.Id;

    public string Title => Group.Title;

    public ObservableCollection<ConversationViewModel> Conversations { get; } = [];
}
