using AIM.Core.PendingActions;
using AIM.Providers;
using AIM.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AIM.Desktop.WinForms;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddAimStorage(context.Configuration);
                services.AddAimProviderCatalog(context.Configuration);
                services.AddSingleton<IPendingAgentActionQueue, FilePendingAgentActionQueue>();
                services.AddSingleton<ChatWindowManager>();
                services.AddSingleton<BuddyListForm>();
            })
            .Build();

        await host.StartAsync();
        await host.Services.GetRequiredService<AimStorageInitializer>().InitializeAsync();

        Application.ApplicationExit += (_, _) => host.StopAsync().GetAwaiter().GetResult();
        Application.Run(host.Services.GetRequiredService<BuddyListForm>());
    }
}
