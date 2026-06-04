using System.Windows;
using AIM.Core.Personalities;
using AIM.Desktop.Wpf.ViewModels;

namespace AIM.Desktop.Wpf.Services;

public interface IChatWindowService
{
    Task OpenFloatingChatAsync(Personality personality);
}

public sealed class ChatWindowService : IChatWindowService
{
    private readonly ChatSessionViewModelFactory _chatSessionFactory;
    private readonly Dictionary<Guid, FloatingChatWindow> _openWindowsByPersonality = [];

    public ChatWindowService(ChatSessionViewModelFactory chatSessionFactory)
    {
        _chatSessionFactory = chatSessionFactory;
    }

    public async Task OpenFloatingChatAsync(Personality personality)
    {
        if (_openWindowsByPersonality.TryGetValue(personality.Id, out var existingWindow))
        {
            if (existingWindow.WindowState == WindowState.Minimized)
            {
                existingWindow.WindowState = WindowState.Normal;
            }

            existingWindow.Activate();
            return;
        }

        var chatSession = _chatSessionFactory.Create(personality);
        await chatSession.LoadAsync();

        var window = new FloatingChatWindow(chatSession)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        _openWindowsByPersonality[personality.Id] = window;
        window.Closed += (_, _) => _openWindowsByPersonality.Remove(personality.Id);
        window.Show();
    }
}
