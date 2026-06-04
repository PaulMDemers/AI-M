using AIM.Core.Chat;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class ChatMessageViewModel : ObservableObject
{
    private string _content;

    public ChatMessageViewModel(ChatRole role, string content, DateTimeOffset createdAt)
    {
        Role = role;
        _content = content;
        CreatedAt = createdAt;
    }

    public ChatRole Role { get; }

    public string RoleLabel => Role switch
    {
        ChatRole.User => "You",
        ChatRole.System => "System",
        ChatRole.Tool => "Tool",
        _ => "AI"
    };

    public bool IsUser => Role == ChatRole.User;

    public string CreatedAtLabel => CreatedAt.ToString("t");

    public string HorizontalAlignment => IsUser ? "Right" : "Left";

    public string BubbleBackground => Role switch
    {
        ChatRole.User => "#DCEFF0",
        ChatRole.System => "#FFF4CE",
        ChatRole.Tool => "#F3E8FF",
        _ => "#EEF2F6"
    };

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public DateTimeOffset CreatedAt { get; }

    public static ChatMessageViewModel FromMessage(ChatMessage message)
    {
        return new ChatMessageViewModel(message.Role, message.Content, message.CreatedAt);
    }
}
