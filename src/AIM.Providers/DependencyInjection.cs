using AIM.Core.Providers;
using AIM.Core.Services;
using AIM.Core.Tools;
using AIM.Providers.Bedrock;
using AIM.Providers.Fakes;
using AIM.Providers.InMemory;
using AIM.Providers.Ollama;
using AIM.Providers.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Providers;

public static class DependencyInjection
{
    public static IServiceCollection AddAimProviderCatalog(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddSingleton<IAiProvider, FakeAiProvider>();
        services.AddSingleton<IProviderDiagnosticsService, ProviderDiagnosticsService>();
        services.AddSingleton<IProviderStatusService, ProviderStatusService>();

        var openAiSettings = OpenAiProviderSettings.FromConfiguration(configuration);
        var hasStoredProviderAccounts = services.Any(service => service.ServiceType == typeof(IProviderAccountService));

        if (hasStoredProviderAccounts)
        {
            services.AddSingleton<IAiProvider>(serviceProvider =>
                new OpenAiProvider(
                    serviceProvider.GetRequiredService<IProviderAccountService>(),
                    openAiSettings));
        }
        else if (openAiSettings.IsConfigured)
        {
            services.AddSingleton<IAiProvider>(_ => new OpenAiProvider(openAiSettings.ApiKey!, openAiSettings.ModelId));
        }

        var ollamaSettings = OllamaProviderSettings.FromConfiguration(configuration);

        if (hasStoredProviderAccounts)
        {
            services.AddSingleton<IAiProvider>(serviceProvider =>
                new OllamaProvider(
                    ollamaSettings,
                    serviceProvider.GetRequiredService<IProviderAccountService>()));
        }
        else if (ollamaSettings.IsConfigured)
        {
            services.AddSingleton<IAiProvider>(_ => new OllamaProvider(ollamaSettings));
        }

        var bedrockSettings = BedrockProviderSettings.FromConfiguration(configuration);

        if (hasStoredProviderAccounts)
        {
            services.AddSingleton<IAiProvider>(serviceProvider =>
                new BedrockProvider(
                    serviceProvider.GetRequiredService<IProviderAccountService>(),
                    bedrockSettings));
        }
        else if (bedrockSettings.IsConfigured)
        {
            services.AddSingleton<IAiProvider>(_ => new BedrockProvider(bedrockSettings));
        }

        return services;
    }

    public static IServiceCollection AddAimPreviewProviders(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddSingleton<IPersonalityService, InMemoryPersonalityService>();
        services.AddSingleton<IConversationService, InMemoryConversationService>();
        services.AddSingleton<IMemoryService, InMemoryMemoryService>();
        services.AddSingleton<IMemorySuggestionService, InMemoryMemorySuggestionService>();
        services.AddSingleton<IAgentToolRegistry, BuiltInAgentToolRegistry>();
        services.AddSingleton<IChatContextBuilder, InMemoryChatContextBuilder>();
        services.AddAimProviderCatalog(configuration);

        return services;
    }
}
