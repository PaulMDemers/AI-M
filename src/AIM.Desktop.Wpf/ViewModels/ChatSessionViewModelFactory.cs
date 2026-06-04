using AIM.Core.Personalities;
using AIM.Core.Providers;
using AIM.Core.Services;
using AIM.Core.Tools;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class ChatSessionViewModelFactory
{
    private readonly IConversationService _conversationService;
    private readonly IPersonalityService _personalityService;
    private readonly IProviderAccountService _providerAccountService;
    private readonly IProviderDiagnosticsService _providerDiagnosticsService;
    private readonly IProviderStatusService _providerStatusService;
    private readonly IMemoryService _memoryService;
    private readonly IMemorySuggestionService _memorySuggestionService;
    private readonly IChatContextBuilder _chatContextBuilder;
    private readonly IAgentToolRegistry _toolRegistry;
    private readonly Services.IProviderSettingsWindowService _providerSettingsWindowService;
    private readonly Services.PendingAgentActionService _pendingAgentActionService;
    private readonly IEnumerable<IAiProvider> _providers;

    public ChatSessionViewModelFactory(
        IConversationService conversationService,
        IPersonalityService personalityService,
        IProviderAccountService providerAccountService,
        IProviderDiagnosticsService providerDiagnosticsService,
        IProviderStatusService providerStatusService,
        IMemoryService memoryService,
        IMemorySuggestionService memorySuggestionService,
        IChatContextBuilder chatContextBuilder,
        IAgentToolRegistry toolRegistry,
        Services.IProviderSettingsWindowService providerSettingsWindowService,
        Services.PendingAgentActionService pendingAgentActionService,
        IEnumerable<IAiProvider> providers)
    {
        _conversationService = conversationService;
        _personalityService = personalityService;
        _providerAccountService = providerAccountService;
        _providerDiagnosticsService = providerDiagnosticsService;
        _providerStatusService = providerStatusService;
        _memoryService = memoryService;
        _memorySuggestionService = memorySuggestionService;
        _chatContextBuilder = chatContextBuilder;
        _toolRegistry = toolRegistry;
        _providerSettingsWindowService = providerSettingsWindowService;
        _pendingAgentActionService = pendingAgentActionService;
        _providers = providers;
    }

    public ChatSessionViewModel Create(Personality personality)
    {
        return new ChatSessionViewModel(
            personality,
            _conversationService,
            _personalityService,
            _providerAccountService,
            _providerDiagnosticsService,
            _providerStatusService,
            _memoryService,
            _memorySuggestionService,
            _chatContextBuilder,
            _toolRegistry,
            _providerSettingsWindowService,
            _pendingAgentActionService,
            _providers);
    }
}
