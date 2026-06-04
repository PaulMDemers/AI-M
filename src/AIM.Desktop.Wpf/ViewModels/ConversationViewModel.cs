using AIM.Core.Chat;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class ConversationViewModel
{
    public ConversationViewModel(Conversation conversation)
    {
        Conversation = conversation;
    }

    public Conversation Conversation { get; }

    public Guid Id => Conversation.Id;

    public string Title => Conversation.Title;

    public string CreatedAt => Conversation.CreatedAtLabel;
}
