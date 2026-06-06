using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIM.Core.Chat;
using AIM.Core.Services;
using AIM.Core.Tools;

namespace AIM.Core.PendingActions;

public sealed record PendingAgentActionDurableToolCall(
    Guid PersonalityId,
    Guid ConversationId,
    AgentToolCall Call);

public sealed record PendingAgentAction(
    Guid Id,
    string Title,
    string Detail,
    DateTimeOffset CreatedAt,
    string SourcePersonality,
    string SourceConversation,
    string SourceKind,
    PendingAgentActionDurableToolCall? ToolCall)
{
    public bool CanApprove => ToolCall is not null;

    public string SourceLabel
    {
        get
        {
            var source = string.IsNullOrWhiteSpace(SourceConversation)
                ? SourcePersonality
                : $"{SourcePersonality} / {SourceConversation}";
            return string.IsNullOrWhiteSpace(source) ? SourceKind : $"{SourceKind} from {source}";
        }
    }

    public string ApprovalNote => CanApprove
        ? "Restored approvals apply the saved tool action; provider follow-up is available from live chat approvals only."
        : "Reopen the conversation to ask the agent to request this action again.";
}

public interface IPendingAgentActionQueue
{
    ObservableCollection<PendingAgentAction> Actions { get; }

    event EventHandler? ActionsChanged;

    void Add(PendingAgentAction action);

    void Remove(Guid actionId);

    PendingAgentAction CreateToolApproval(
        string title,
        string detail,
        string sourcePersonality,
        string sourceConversation,
        string sourceKind,
        Guid personalityId,
        Guid conversationId,
        AgentToolCall call);

    Task<PendingAgentActionResult> ApproveAsync(
        PendingAgentAction action,
        CancellationToken cancellationToken = default);
}

public sealed record PendingAgentActionResult(string Summary, ChatMessage? ToolMessage = null);

public sealed class FilePendingAgentActionQueue : IPendingAgentActionQueue
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _storagePath;
    private readonly IPersonalityService? _personalityService;
    private readonly IConversationService? _conversationService;
    private readonly IAgentToolRegistry? _toolRegistry;

    public FilePendingAgentActionQueue(
        IPersonalityService personalityService,
        IConversationService conversationService,
        IAgentToolRegistry toolRegistry)
        : this(null, personalityService, conversationService, toolRegistry)
    {
    }

    public FilePendingAgentActionQueue(
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

    public ObservableCollection<PendingAgentAction> Actions { get; } = [];

    public event EventHandler? ActionsChanged;

    public void Add(PendingAgentAction action)
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

    public PendingAgentAction CreateToolApproval(
        string title,
        string detail,
        string sourcePersonality,
        string sourceConversation,
        string sourceKind,
        Guid personalityId,
        Guid conversationId,
        AgentToolCall call)
    {
        return new PendingAgentAction(
            Guid.NewGuid(),
            title,
            detail,
            DateTimeOffset.Now,
            sourcePersonality,
            sourceConversation,
            sourceKind,
            new PendingAgentActionDurableToolCall(
                personalityId,
                conversationId,
                new AgentToolCall(call.Id, call.Name, CloneArguments(call.Arguments))));
    }

    public async Task<PendingAgentActionResult> ApproveAsync(
        PendingAgentAction action,
        CancellationToken cancellationToken = default)
    {
        if (action.ToolCall is null ||
            _personalityService is null ||
            _conversationService is null ||
            _toolRegistry is null)
        {
            return new PendingAgentActionResult("This pending action cannot be approved after restart.");
        }

        var personality = await _personalityService.GetAsync(action.ToolCall.PersonalityId, cancellationToken);
        var conversation = await _conversationService.GetConversationAsync(action.ToolCall.ConversationId, cancellationToken);

        if (personality is null || conversation is null)
        {
            Remove(action.Id);
            return new PendingAgentActionResult("The original personality or conversation no longer exists.");
        }

        var call = new AgentToolCall(
            action.ToolCall.Call.Id,
            action.ToolCall.Call.Name,
            CloneArguments(action.ToolCall.Call.Arguments));
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

        Remove(action.Id);
        return new PendingAgentActionResult(result.Content);
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
                Actions.Add(snapshot.ToAction());
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

        var snapshots = Actions.Select(PendingAgentActionSnapshot.FromAction).ToArray();
        using var stream = File.Create(_storagePath);
        JsonSerializer.Serialize(stream, snapshots, SerializerOptions);
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
        DurableToolCallSnapshot? ToolCall)
    {
        public static PendingAgentActionSnapshot FromAction(PendingAgentAction action)
        {
            return new PendingAgentActionSnapshot(
                action.Id,
                action.Title,
                action.Detail,
                action.CreatedAt,
                action.SourcePersonality,
                action.SourceConversation,
                action.SourceKind,
                action.ToolCall is null
                    ? null
                    : new DurableToolCallSnapshot(
                        action.ToolCall.PersonalityId,
                        action.ToolCall.ConversationId,
                        action.ToolCall.Call.Id,
                        action.ToolCall.Call.Name,
                        CloneArguments(action.ToolCall.Call.Arguments)));
        }

        public PendingAgentAction ToAction()
        {
            return new PendingAgentAction(
                Id,
                Title,
                Detail,
                CreatedAt,
                SourcePersonality,
                SourceConversation,
                SourceKind,
                ToolCall is null
                    ? null
                    : new PendingAgentActionDurableToolCall(
                        ToolCall.PersonalityId,
                        ToolCall.ConversationId,
                        new AgentToolCall(
                            ToolCall.CallId,
                            ToolCall.Name,
                            CloneArguments(ToolCall.Arguments))));
        }
    }

    private sealed record DurableToolCallSnapshot(
        Guid PersonalityId,
        Guid ConversationId,
        string CallId,
        string Name,
        JsonObject Arguments);
}
