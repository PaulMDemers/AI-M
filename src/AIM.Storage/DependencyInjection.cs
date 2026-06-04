using AIM.Core.Services;
using AIM.Core.Tools;
using AIM.Storage.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddAimStorage(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var settings = AimStorageSettings.FromConfiguration(configuration);

        services.AddSingleton(settings);
        services.AddDbContextFactory<AimDbContext>(options => options.UseSqlite(settings.ConnectionString));
        services.AddSingleton<AimStorageInitializer>();
        services.AddSingleton<IPersonalityService, SqlitePersonalityService>();
        services.AddSingleton<IConversationService, SqliteConversationService>();
        services.AddSingleton<IMemoryService, SqliteMemoryService>();
        services.AddSingleton<IMemorySuggestionService, SqliteMemorySuggestionService>();
        services.AddSingleton<IAgentToolRegistry, BuiltInAgentToolRegistry>();
        services.AddSingleton<IChatContextBuilder, LatestMemoryChatContextBuilder>();
        services.AddSingleton<IProviderAccountService, SqliteProviderAccountService>();

        return services;
    }
}
