using AIM.Core.Personalities;
using AIM.Core.Providers;
using AIM.Core.Services;
using AIM.Core.Tools;

namespace AIM.Desktop.WinForms;

internal sealed class ChatWindowManager
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
    private readonly PendingAgentActionService _pendingAgentActionService;
    private readonly IEnumerable<IAiProvider> _providers;
    private readonly Dictionary<Guid, ChatForm> _openWindows = [];

    public ChatWindowManager(
        IConversationService conversationService,
        IPersonalityService personalityService,
        IProviderAccountService providerAccountService,
        IProviderDiagnosticsService providerDiagnosticsService,
        IProviderStatusService providerStatusService,
        IMemoryService memoryService,
        IMemorySuggestionService memorySuggestionService,
        IChatContextBuilder chatContextBuilder,
        IAgentToolRegistry toolRegistry,
        PendingAgentActionService pendingAgentActionService,
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
        _pendingAgentActionService = pendingAgentActionService;
        _providers = providers;
    }

    public async Task OpenAsync(Personality personality)
    {
        if (_openWindows.TryGetValue(personality.Id, out var existing) && !existing.IsDisposed)
        {
            existing.Show();
            existing.Activate();
            return;
        }

        var form = new ChatForm(
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
            _pendingAgentActionService,
            _providers);
        form.FormClosed += (_, _) => _openWindows.Remove(personality.Id);
        _openWindows[personality.Id] = form;
        await form.LoadConversationAsync();
        form.Show();
    }
}
