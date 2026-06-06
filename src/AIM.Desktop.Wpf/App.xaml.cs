using System.Windows;
using AIM.Core.PendingActions;
using AIM.Desktop.Wpf.Services;
using AIM.Desktop.Wpf.ViewModels;
using AIM.Providers;
using AIM.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Forms = System.Windows.Forms;

namespace AIM.Desktop.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private Forms.NotifyIcon? _trayIcon;
    private bool _isExiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder(e.Args)
            .ConfigureServices((context, services) =>
            {
                services.AddAimStorage(context.Configuration);
                services.AddAimProviderCatalog(context.Configuration);
                services.AddSingleton<ChatSessionViewModelFactory>();
                services.AddSingleton<IChatWindowService, ChatWindowService>();
                services.AddSingleton<FirstRunSetupPreferenceService>();
                services.AddSingleton<IFirstRunSetupService, FirstRunSetupService>();
                services.AddSingleton<IProviderSettingsWindowService, ProviderSettingsWindowService>();
                services.AddSingleton<IPersonalityEditorWindowService, PersonalityEditorWindowService>();
                services.AddSingleton<IMemoryReviewWindowService, MemoryReviewWindowService>();
                services.AddSingleton<IPendingAgentActionQueue, FilePendingAgentActionQueue>();
                services.AddSingleton<PendingAgentActionService>();
                services.AddSingleton<IPendingActionsReviewWindowService, PendingActionsReviewWindowService>();
                services.AddTransient<FirstRunSetupViewModel>();
                services.AddTransient<ProviderSettingsViewModel>();
                services.AddTransient<PersonalityEditorViewModel>();
                services.AddTransient<MemoryReviewViewModel>();
                services.AddTransient<PendingActionsReviewViewModel>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();
        await _host.Services.GetRequiredService<AimStorageInitializer>().InitializeAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        InitializeTrayIcon(mainWindow);
        mainWindow.Show();
        await _host.Services.GetRequiredService<IFirstRunSetupService>().ShowIfNeededAsync(mainWindow);

        if (mainWindow.DataContext is MainWindowViewModel mainViewModel)
        {
            await mainViewModel.RefreshFriendsAsync();
        }

        base.OnStartup(e);
    }

    private void InitializeTrayIcon(MainWindow mainWindow)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open AI-M", null, (_, _) => ShowMainWindow(mainWindow));
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _isExiting = true;
            Shutdown();
        });

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "AI-M",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow(mainWindow);

        mainWindow.StateChanged += (_, _) =>
        {
            if (mainWindow.WindowState == WindowState.Minimized)
            {
                mainWindow.Hide();
            }
        };

        mainWindow.Closing += (sender, args) =>
        {
            if (_isExiting)
            {
                return;
            }

            args.Cancel = true;
            mainWindow.Hide();
        };
    }

    private static void ShowMainWindow(Window mainWindow)
    {
        mainWindow.Show();
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
