using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIM.Core.Chat;
using AIM.Core.Services;
using AIM.Core.Tools;
using AIM.Desktop.Wpf.ViewModels;

namespace AIM.Desktop.Wpf.Services;

public sealed class PendingAgentActionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _storagePath;
    private readonly IPersonalityService? _personalityService;
    private readonly IConversationService? _conversationService;
    private readonly IAgentToolRegistry? _toolRegistry;

    public PendingAgentActionService(
        IPersonalityService personalityService,
        IConversationService conversationService,
        IAgentToolRegistry toolRegistry)
        : this(null, personalityService, conversationService, toolRegistry)
    {
    }

    public PendingAgentActionService(
        string? storagePath = null,
        IPersonalityService? personalityService = null,
        IConversationService? conversationService = null,
        IAgentToolRegistry? toolRegistry = null)
    {
        _storagePath = storagePath ?? BuildDefaultStoragePath();
        _personalityService = personalityService;
        _conversationService = conversationService;
        _toolRegistry = toolRegistry;
        Load();
    }

    public ObservableCollection<PendingAgentActionViewModel> Actions { get; } = [];

    public event EventHandler? ActionsChanged;

    public void Add(PendingAgentActionViewModel action)
    {
        if (Actions.Any(existing => existing.Id == action.Id))
        {
            return;
        }

        Actions.Add(action);
        Save();
        ActionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(Guid actionId)
    {
        var action = Actions.FirstOrDefault(candidate => candidate.Id == actionId);

        if (action is null)
        {
            return;
        }

        Actions.Remove(action);
        Save();
        ActionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string BuildDefaultStoragePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "AI-M", "pending-actions.json");
    }

    private void Load()
    {
        if (!File.Exists(_storagePath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(_storagePath);
            var snapshots = JsonSerializer.Deserialize<IReadOnlyList<PendingAgentActionSnapshot>>(
                stream,
                SerializerOptions) ?? [];

            foreach (var snapshot in snapshots)
            {
                var durableToolCall = snapshot.ToolCall is null
                    ? null
                    : new PendingAgentActionDurableToolCall(
                        snapshot.ToolCall.PersonalityId,
                        snapshot.ToolCall.ConversationId,
                        new AgentToolCall(
                            snapshot.ToolCall.CallId,
                            snapshot.ToolCall.Name,
                            CloneArguments(snapshot.ToolCall.Arguments)));
                var canApprove = durableToolCall is not null &&
                    _personalityService is not null &&
                    _conversationService is not null &&
                    _toolRegistry is not null;
                var approvalUnavailableText = durableToolCall is null
                    ? "Restarted app. Reopen the conversation to ask the agent to request this action again."
                    : "Restarted app. Provider follow-up is unavailable, but the saved tool action can still be approved.";
                var action = new PendingAgentActionViewModel(
                    snapshot.Title,
                    snapshot.Detail,
                    cancellationToken => ApproveRestoredToolActionAsync(durableToolCall, cancellationToken),
                    snapshot.Id,
                    snapshot.CreatedAt,
                    canApprove,
                    approvalUnavailableText,
                    durableToolCall);
                action.AttachSource(snapshot.SourcePersonality, snapshot.SourceConversation, snapshot.SourceKind);
                action.AttachHandlers(async () =>
                {
                    if (action.CanApprove)
                    {
                        await ApproveRestoredActionFromQueueAsync(action);
                    }
                }, () => Remove(action.Id));
                Actions.Add(action);
            }
        }
        catch (IOException)
        {
            Actions.Clear();
        }
        catch (JsonException)
        {
            Actions.Clear();
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var snapshots = Actions
            .Select(action => new PendingAgentActionSnapshot(
                action.Id,
                action.Title,
                action.Detail,
                action.CreatedAt,
                action.SourcePersonality,
                action.SourceConversation,
                action.SourceKind,
                action.DurableToolCall is null
                    ? null
                    : new DurableToolCallSnapshot(
                        action.DurableToolCall.PersonalityId,
                        action.DurableToolCall.ConversationId,
                        action.DurableToolCall.Call.Id,
                        action.DurableToolCall.Call.Name,
                        CloneArguments(action.DurableToolCall.Call.Arguments))))
            .ToArray();

        using var stream = File.Create(_storagePath);
        JsonSerializer.Serialize(stream, snapshots, SerializerOptions);
    }

    private async Task ApproveRestoredActionFromQueueAsync(PendingAgentActionViewModel action)
    {
        if (action.IsBusy || !action.CanApprove)
        {
            return;
        }

        action.IsBusy = true;

        try
        {
            await action.ApproveAsync(CancellationToken.None);
            Remove(action.Id);
        }
        finally
        {
            action.IsBusy = false;
        }
    }

    private async Task<PendingAgentActionResult> ApproveRestoredToolActionAsync(
        PendingAgentActionDurableToolCall? durableToolCall,
        CancellationToken cancellationToken)
    {
        if (durableToolCall is null ||
            _personalityService is null ||
            _conversationService is null ||
            _toolRegistry is null)
        {
            return new PendingAgentActionResult("This pending action cannot be approved after restart.");
        }

        var personality = await _personalityService.GetAsync(durableToolCall.PersonalityId, cancellationToken);
        var conversation = await _conversationService.GetConversationAsync(durableToolCall.ConversationId, cancellationToken);

        if (personality is null || conversation is null)
        {
            return new PendingAgentActionResult("The original personality or conversation no longer exists.");
        }

        var call = new AgentToolCall(
            durableToolCall.Call.Id,
            durableToolCall.Call.Name,
            CloneArguments(durableToolCall.Call.Arguments));
        var result = await _toolRegistry.ExecuteAsync(
            call,
            new AgentToolContext(personality, conversation),
            cancellationToken);
        await _conversationService.AddMessageAsync(
            conversation.Id,
            ChatRole.System,
            $"Pending action approved after restart: {call.Name}. Result: {result.Content}",
            cancellationToken);
        await _conversationService.AddMessageAsync(
            conversation.Id,
            ChatRole.Tool,
            FormatToolResults([result]),
            cancellationToken);

        return new PendingAgentActionResult(result.Content);
    }

    private static JsonObject CloneArguments(JsonObject arguments)
    {
        return arguments.DeepClone().AsObject();
    }

    private static string FormatToolResults(IReadOnlyList<AgentToolResult> results)
    {
        var lines = results.Select(result =>
            $"- id={result.Id}; name={result.Name}; success={result.Success}; result={result.Content}");
        return string.Join('\n', lines);
    }

    private sealed record PendingAgentActionSnapshot(
        Guid Id,
        string Title,
        string Detail,
        DateTimeOffset CreatedAt,
        string SourcePersonality,
        string SourceConversation,
        string SourceKind,
        DurableToolCallSnapshot? ToolCall);

    private sealed record DurableToolCallSnapshot(
        Guid PersonalityId,
        Guid ConversationId,
        string CallId,
        string Name,
        JsonObject Arguments);
}
