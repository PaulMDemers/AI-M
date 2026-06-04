using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AIM.Desktop.Wpf.ViewModels;

namespace AIM.Desktop.Wpf.Views;

public partial class ChatSurface : System.Windows.Controls.UserControl
{
    private ChatSessionViewModel? _chatSession;

    public ChatSurface()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachChatSession();
        _chatSession = e.NewValue as ChatSessionViewModel;
        AttachChatSession();
        ScrollMessagesToBottom();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachChatSession();
        _chatSession = null;
    }

    private async void MessageInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        e.Handled = true;

        if (_chatSession?.SendMessageCommand.CanExecute(null) == true)
        {
            await _chatSession.SendMessageCommand.ExecuteAsync(null);
        }
    }

    private void AttachChatSession()
    {
        if (_chatSession is null)
        {
            return;
        }

        _chatSession.Messages.CollectionChanged += OnMessagesChanged;

        foreach (var message in _chatSession.Messages)
        {
            message.PropertyChanged += OnMessagePropertyChanged;
        }
    }

    private void DetachChatSession()
    {
        if (_chatSession is null)
        {
            return;
        }

        _chatSession.Messages.CollectionChanged -= OnMessagesChanged;

        foreach (var message in _chatSession.Messages)
        {
            message.PropertyChanged -= OnMessagePropertyChanged;
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<ChatMessageViewModel>())
            {
                item.PropertyChanged -= OnMessagePropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<ChatMessageViewModel>())
            {
                item.PropertyChanged += OnMessagePropertyChanged;
            }
        }

        ScrollMessagesToBottom();
    }

    private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatMessageViewModel.Content))
        {
            ScrollMessagesToBottom();
        }
    }

    private void ScrollMessagesToBottom()
    {
        Dispatcher.BeginInvoke(() => MessagesScrollViewer.ScrollToEnd());
    }
}
