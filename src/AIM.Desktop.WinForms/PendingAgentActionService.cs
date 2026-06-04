using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIM.Core.Chat;
using AIM.Core.Services;
using AIM.Core.Tools;

namespace AIM.Desktop.WinForms;

internal sealed class PendingAgentActionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _storagePath;
    private readonly IPersonalityService _personalityService;
    private readonly IConversationService _conversationService;
    private readonly IAgentToolRegistry _toolRegistry;

    public PendingAgentActionService(
        IPersonalityService personalityService,
        IConversationService conversationService,
        IAgentToolRegistry toolRegistry)
    {
        _personalityService = personalityService;
        _conversationService = conversationService;
        _toolRegistry = toolRegistry;
        _storagePath = BuildDefaultStoragePath();
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

    public async Task ApproveAsync(PendingAgentAction action, CancellationToken cancellationToken = default)
    {
        if (action.ToolCall is null)
        {
            return;
        }

        var personality = await _personalityService.GetAsync(action.ToolCall.PersonalityId, cancellationToken);
        var conversation = await _conversationService.GetConversationAsync(action.ToolCall.ConversationId, cancellationToken);

        if (personality is null || conversation is null)
        {
            Remove(action.Id);
            return;
        }

        var call = new AgentToolCall(
            action.ToolCall.CallId,
            action.ToolCall.Name,
            CloneArguments(action.ToolCall.Arguments));
        var result = await _toolRegistry.ExecuteAsync(call, new AgentToolContext(personality, conversation), cancellationToken);
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
            new DurableToolCallSnapshot(
                personalityId,
                conversationId,
                call.Id,
                call.Name,
                CloneArguments(call.Arguments)));
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
            var snapshots = JsonSerializer.Deserialize<IReadOnlyList<PendingAgentAction>>(
                stream,
                SerializerOptions) ?? [];

            foreach (var snapshot in snapshots)
            {
                Actions.Add(snapshot);
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

        using var stream = File.Create(_storagePath);
        JsonSerializer.Serialize(stream, Actions.ToArray(), SerializerOptions);
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
}

internal sealed record PendingAgentAction(
    Guid Id,
    string Title,
    string Detail,
    DateTimeOffset CreatedAt,
    string SourcePersonality,
    string SourceConversation,
    string SourceKind,
    DurableToolCallSnapshot? ToolCall)
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

internal sealed record DurableToolCallSnapshot(
    Guid PersonalityId,
    Guid ConversationId,
    string CallId,
    string Name,
    JsonObject Arguments);
