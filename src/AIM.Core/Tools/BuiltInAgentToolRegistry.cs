using System.Text.Json;
using System.Text.Json.Nodes;
using AIM.Core.Chat;
using AIM.Core.Personalities;
using AIM.Core.Services;

namespace AIM.Core.Tools;

public sealed class BuiltInAgentToolRegistry : IAgentToolRegistry
{
    private readonly IMemoryService _memoryService;
    private readonly IPersonalityService _personalityService;
    private readonly IConversationService _conversationService;

    public BuiltInAgentToolRegistry(
        IMemoryService memoryService,
        IPersonalityService personalityService,
        IConversationService conversationService)
    {
        _memoryService = memoryService;
        _personalityService = personalityService;
        _conversationService = conversationService;
    }

    public IReadOnlyList<AgentToolDefinition> ListTools() =>
    [
        new(
            "memory.list",
            "List approved memories for the current personality.",
            """{"type":"object","properties":{},"additionalProperties":false}"""),
        new(
            "memory.search",
            "Search approved memories for the current personality by text.",
            """{"type":"object","properties":{"query":{"type":"string"},"limit":{"type":"integer","minimum":1,"maximum":20}},"required":["query"],"additionalProperties":false}"""),
        new(
            "memory.remember",
            "Store a durable memory for the current personality.",
            """{"type":"object","properties":{"content":{"type":"string"}},"required":["content"],"additionalProperties":false}""",
            RequiresApproval: true),
        new(
            "memory.forget",
            "Delete approved memories for the current personality that match a text fragment.",
            """{"type":"object","properties":{"match":{"type":"string"}},"required":["match"],"additionalProperties":false}""",
            RequiresApproval: true),
        new(
            "personality.get",
            "Get the current personality profile, status, model, and system prompt.",
            """{"type":"object","properties":{},"additionalProperties":false}"""),
        new(
            "personality.update_status",
            "Update the current personality status text.",
            """{"type":"object","properties":{"status":{"type":"string","maxLength":80}},"required":["status"],"additionalProperties":false}""",
            RequiresApproval: true),
        new(
            "personality.append_system_note",
            "Append a durable note to the current personality system prompt.",
            """{"type":"object","properties":{"note":{"type":"string"}},"required":["note"],"additionalProperties":false}""",
            RequiresApproval: true),
        new(
            "conversation.list",
            "List conversation groups and conversations for the current personality.",
            """{"type":"object","properties":{},"additionalProperties":false}"""),
        new(
            "conversation.recent",
            "Get recent visible messages from the active conversation.",
            """{"type":"object","properties":{"limit":{"type":"integer","minimum":1,"maximum":50}},"additionalProperties":false}"""),
        new(
            "conversation.search",
            "Search visible messages in the active conversation by text.",
            """{"type":"object","properties":{"query":{"type":"string"},"limit":{"type":"integer","minimum":1,"maximum":50}},"required":["query"],"additionalProperties":false}"""),
        new(
            "conversation.summary.get",
            "Get the durable summary for the active conversation.",
            """{"type":"object","properties":{},"additionalProperties":false}"""),
        new(
            "conversation.summary.update",
            "Replace the durable summary for the active conversation.",
            """{"type":"object","properties":{"summary":{"type":"string"}},"required":["summary"],"additionalProperties":false}""",
            RequiresApproval: true),
        new(
            "time.now",
            "Get the current local timestamp.",
            """{"type":"object","properties":{},"additionalProperties":false}""")
    ];

    public async Task<AgentToolResult> ExecuteAsync(
        AgentToolCall call,
        AgentToolContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return call.Name switch
            {
                "memory.list" => await ListMemoriesAsync(call, context, cancellationToken),
                "memory.search" => await SearchMemoriesAsync(call, context, cancellationToken),
                "memory.remember" => await RememberAsync(call, context, cancellationToken),
                "memory.forget" => await ForgetAsync(call, context, cancellationToken),
                "personality.get" => GetPersonality(call, context),
                "personality.update_status" => await UpdatePersonalityStatusAsync(call, context, cancellationToken),
                "personality.append_system_note" => await AppendPersonalitySystemNoteAsync(call, context, cancellationToken),
                "conversation.list" => await ListConversationsAsync(call, context, cancellationToken),
                "conversation.recent" => await RecentConversationMessagesAsync(call, context, cancellationToken),
                "conversation.search" => await SearchConversationMessagesAsync(call, context, cancellationToken),
                "conversation.summary.get" => await GetConversationSummaryAsync(call, context, cancellationToken),
                "conversation.summary.update" => await UpdateConversationSummaryAsync(call, context, cancellationToken),
                "time.now" => TimeNow(call),
                _ => new AgentToolResult(call.Id, call.Name, false, $"Unknown tool '{call.Name}'.")
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return new AgentToolResult(call.Id, call.Name, false, ex.Message);
        }
    }

    private async Task<AgentToolResult> ListMemoriesAsync(
        AgentToolCall call,
        AgentToolContext context,
        CancellationToken cancellationToken)
    {
        var memories = await _memoryService.GetMemoriesAsync(context.Personality.Id, cancellationToken);
        var result = memories.Select(memory => new
        {
            id = memory.Id,
            memory.Content,
            createdAt = memory.CreatedAt
        });

        return Success(call, result);
    }

    private async Task<AgentToolResult> SearchMemoriesAsync(
        AgentToolCall call,
        AgentToolContext context,
        CancellationToken cancellationToken)
    {
        var query = RequiredString(call.Arguments, "query");
        var limit = OptionalInt(call.Arguments, "limit", defaultValue: 8, min: 1, max: 20);
        var terms = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var memories = await _memoryService.GetMemoriesAsync(context.Personality.Id, cancellationToken);
        var matches = memories
            .Select(memory => new
            {
                Memory = memory,
                Score = ScoreMemory(memory.Content, query, terms)
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Memory.CreatedAt)
            .Take(limit)
            .Select(match => new
            {
                id = match.Memory.Id,
                content = match.Memory.Content,
                createdAt = match.Memory.CreatedAt
            })
            .ToArray();

        return Success(call, new
        {
            query,
            count = matches.Length,
            memories = matches
        });
    }

    private async Task<AgentToolResult> RememberAsync(
        AgentToolCall call,
        AgentToolContext context,
        CancellationToken cancellationToken)
    {
        var content = RequiredString(call.Arguments, "content");
        var memories = await _memoryService.GetMemoriesAsync(context.Personality.Id, cancellationToken);

        if (memories.Any(memory => string.Equals(memory.Content, content, StringComparison.OrdinalIgnoreCase)))
        {
            return new AgentToolResult(call.Id, call.Name, true, "Memory already exists.");
        }

        var memory = await _memoryService.RememberAsync(context.Personality.Id, content, cancellationToken);

        return Success(call, new { id = memory.Id, memory.Content });
    }

    private async Task<AgentToolResult> ForgetAsync(
        AgentToolCall call,
        AgentToolContext context,
        CancellationToken cancellationToken)
    {
        var match = RequiredString(call.Arguments, "match");
        var memories = await _memoryService.GetMemoriesAsync(context.Personality.Id, cancellationToken);
        var matched = memories
            .Where(memory =>
                string.Equals(memory.Content, match, StringComparison.OrdinalIgnoreCase) ||
                memory.Content.Contains(match, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var memory in matched)
        {
            await _memoryService.DeleteAsync(memory.Id, cancellationToken);
        }

        return Success(call, new { deleted = matched.Length });
    }

    private static AgentToolResult GetPersonality(AgentToolCall call, AgentToolContext context)
    {
        return Success(call, new
        {
            context.Personality.Id,
            context.Personality.DisplayName,
            context.Personality.Status,
            context.Personality.AvatarText,
            context.Personality.AvatarImagePath,
            context.Personality.Category,
            context.Personality.SystemPrompt,
            context.Personality.ProviderKey,
            context.Personality.ModelId
        });
    }

    private async Task<AgentToolResult> UpdatePersonalityStatusAsync(
        AgentToolCall call,
        AgentToolContext context,
        CancellationToken cancellationToken)
    {
        var status = Truncate(RequiredString(call.Arguments, "status"), 80);
        var personality = await GetLatestPersonalityAsync(context, cancellationToken);
        var updated = await _personalityService.SaveAsync(
            ToDraft(personality, status: status),
            cancellationToken);

        return Success(call, new
        {
            updated.Id,
            updated.Status
        });
    }

    private async Task<AgentToolResult> AppendPersonalitySystemNoteAsync(
        AgentToolCall call,
        AgentToolContext context,
        CancellationToken cancellationToken)
    {
        var note = RequiredString(call.Arguments, "note");
        var personality = await GetLatestPersonalityAsync(context, cancellationToken);
        var systemPrompt = personality.SystemPrompt.Contains(note, StringComparison.OrdinalIgnoreCase)
            ? personality.SystemPrompt
            : $"{personality.SystemPrompt.Trim()}\n\nSelf-updated note: {note}";
        var updated = await _personalityService.SaveAsync(
            ToDraft(personality, systemPrompt: systemPrompt),
            cancellationToken);

        return Success(call, new
        {
            updated.Id,
            changed = !string.Equals(personality.SystemPrompt, updated.SystemPrompt, StringComparison.Ordinal),
            updated.SystemPrompt
        });
    }

    private async Task<AgentToolResult> ListConversationsAsync(
        AgentToolCall call,
        AgentToolContext context,
        CancellationToken cancellationToken)
    {
        var groups = await _conversationService.ListConversationGroupsAsync(context.Personality.Id, cancellationToken);
        var result = new List<object>();

        foreach (var group in groups)
        {
            var conversations = await _conversationService.ListConversationsAsync(
                context.Personality.Id,
                group.Id,
                cancellationToken);
            result.Add(new
            {
                group.Id,
                group.Title,
                conversations = conversations.Select(conversation => new
                {
                    conversation.Id,
                    conversation.Title,
                    conversation.CreatedAt
                })
            });
        }

        return Success(call, result);
    }

    private async Task<AgentToolResult> RecentConversationMessagesAsync(
        AgentToolCall call,
        AgentToolContext context,
        CancellationToken cancellationToken)
    {
        var limit = OptionalInt(call.Arguments, "limit", defaultValue: 12, min: 1, max: 50);
        var messages = await _conversationService.GetMessagesAsync(context.Conversation.Id, cancellationToken);
        var visible = messages
            .Where(IsVisibleMessage)
            .OrderByDescending(message => message.CreatedAt)
            .Take(limit)
            .OrderBy(message => message.CreatedAt)
            .Select(ToMessageResult)
            .ToArray();

        return Success(call, new
        {
            context.Conversation.Id,
            context.Conversation.Title,
            count = visible.Length,
            messages = visible
        });
    }

    private async Task<AgentToolResult> SearchConversationMessagesAsync(
        AgentToolCall call,
        AgentToolContext context,
        CancellationToken cancellationToken)
    {
        var query = RequiredString(call.Arguments, "query");
        var limit = OptionalInt(call.Arguments, "limit", defaultValue: 12, min: 1, max: 50);
        var terms = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var messages = await _conversationService.GetMessagesAsync(context.Conversation.Id, cancellationToken);
        var matches = messages
            .Where(IsVisibleMessage)
            .Select(message => new
            {
                Message = message,
                Score = ScoreText(message.Content, query, terms)
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Message.CreatedAt)
            .Take(limit)
            .Select(match => ToMessageResult(match.Message))
            .ToArray();

        return Success(call, new
        {
            query,
            count = matches.Length,
            messages = matches
        });
    }

    private async Task<AgentToolResult> GetConversationSummaryAsync(
        AgentToolCall call,
        AgentToolContext context,
        CancellationToken cancellationToken)
    {
        var conversation = await _conversationService.GetConversationAsync(context.Conversation.Id, cancellationToken) ??
            context.Conversation;

        return Success(call, new
        {
            conversation.Id,
            conversation.Title,
            conversation.Summary,
            conversation.SummaryUpdatedAt
        });
    }

    private async Task<AgentToolResult> UpdateConversationSummaryAsync(
        AgentToolCall call,
        AgentToolContext context,
        CancellationToken cancellationToken)
    {
        var summary = RequiredString(call.Arguments, "summary");
        await _conversationService.UpdateConversationSummaryAsync(
            context.Conversation.Id,
            summary,
            cancellationToken);
        var conversation = await _conversationService.GetConversationAsync(context.Conversation.Id, cancellationToken) ??
            context.Conversation;

        return Success(call, new
        {
            conversation.Id,
            conversation.Title,
            conversation.Summary,
            conversation.SummaryUpdatedAt
        });
    }

    private static AgentToolResult TimeNow(AgentToolCall call)
    {
        return Success(call, new
        {
            local = DateTimeOffset.Now,
            utc = DateTimeOffset.UtcNow
        });
    }

    private static string RequiredString(JsonObject arguments, string name)
    {
        var value = arguments[name]?.GetValue<string>()?.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Tool argument '{name}' is required.");
        }

        return value;
    }

    private async Task<Personality> GetLatestPersonalityAsync(
        AgentToolContext context,
        CancellationToken cancellationToken)
    {
        return await _personalityService.GetAsync(context.Personality.Id, cancellationToken) ??
            context.Personality;
    }

    private static PersonalityDraft ToDraft(
        Personality personality,
        string? status = null,
        string? systemPrompt = null)
    {
        return new PersonalityDraft(
            personality.Id,
            personality.DisplayName,
            status ?? personality.Status,
            personality.AvatarText,
            systemPrompt ?? personality.SystemPrompt,
            personality.ProviderKey,
            personality.ModelId,
            personality.AvatarImagePath,
            personality.Category);
    }

    private static int OptionalInt(JsonObject arguments, string name, int defaultValue, int min, int max)
    {
        var node = arguments[name];

        if (node is null)
        {
            return defaultValue;
        }

        try
        {
            return Math.Clamp(node.GetValue<int>(), min, max);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw new ArgumentException($"Tool argument '{name}' must be an integer.");
        }
    }

    private static int ScoreMemory(string content, string query, IReadOnlyList<string> terms)
    {
        return ScoreText(content, query, terms);
    }

    private static int ScoreText(string content, string query, IReadOnlyList<string> terms)
    {
        var score = content.Contains(query, StringComparison.OrdinalIgnoreCase) ? 100 : 0;

        foreach (var term in terms)
        {
            if (content.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score++;
            }
        }

        return score;
    }

    private static bool IsVisibleMessage(ChatMessage message)
    {
        return message.Role is ChatRole.User or ChatRole.Assistant;
    }

    private static object ToMessageResult(ChatMessage message)
    {
        return new
        {
            id = message.Id,
            role = message.Role.ToString().ToLowerInvariant(),
            content = message.Content,
            createdAt = message.CreatedAt
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].Trim();
    }

    private static AgentToolResult Success(AgentToolCall call, object value)
    {
        return new AgentToolResult(call.Id, call.Name, true, JsonSerializer.Serialize(value));
    }
}
