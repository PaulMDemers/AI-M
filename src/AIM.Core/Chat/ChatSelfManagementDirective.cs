using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AIM.Core.Chat;

public sealed record ChatSelfManagementDirective(
    IReadOnlyList<MemoryDirective> Memories,
    PersonalityDirective? Personality)
{
    public static ChatSelfManagementDirective Empty { get; } = new([], null);

    public bool HasChanges => Memories.Count > 0 || Personality is not null;
}

public sealed record MemoryDirective(
    string Action,
    string Content,
    string? OldContent = null);

public sealed record PersonalityDirective(
    string? Status = null,
    string? SystemPrompt = null,
    string? SystemPromptAppend = null);

public sealed record ChatSelfManagementExtraction(
    string VisibleContent,
    ChatSelfManagementDirective Directive);

public static partial class ChatSelfManagementDirectiveParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static ChatSelfManagementExtraction Extract(string content)
    {
        var match = ManagementBlockRegex().Match(content);

        if (!match.Success)
        {
            return new ChatSelfManagementExtraction(content, ChatSelfManagementDirective.Empty);
        }

        var visibleContent = ManagementBlockRegex().Replace(content, string.Empty).TrimEnd();
        var json = match.Groups["json"].Value;

        try
        {
            var dto = JsonSerializer.Deserialize<DirectiveDto>(json, SerializerOptions);
            return new ChatSelfManagementExtraction(visibleContent, Map(dto));
        }
        catch (JsonException)
        {
            return new ChatSelfManagementExtraction(visibleContent, ChatSelfManagementDirective.Empty);
        }
    }

    private static ChatSelfManagementDirective Map(DirectiveDto? dto)
    {
        if (dto is null)
        {
            return ChatSelfManagementDirective.Empty;
        }

        var memories = dto.Memories?
            .Select(memory => new MemoryDirective(
                NormalizeAction(memory.Action),
                Normalize(memory.Content),
                Normalize(memory.OldContent)))
            .Where(memory => IsSupportedMemoryAction(memory.Action) && !string.IsNullOrWhiteSpace(memory.Content))
            .ToArray() ?? [];
        var personality = dto.Personality is null
            ? null
            : new PersonalityDirective(
                Normalize(dto.Personality.Status),
                Normalize(dto.Personality.SystemPrompt),
                Normalize(dto.Personality.SystemPromptAppend));

        if (personality is not null &&
            string.IsNullOrWhiteSpace(personality.Status) &&
            string.IsNullOrWhiteSpace(personality.SystemPrompt) &&
            string.IsNullOrWhiteSpace(personality.SystemPromptAppend))
        {
            personality = null;
        }

        return new ChatSelfManagementDirective(memories, personality);
    }

    private static string NormalizeAction(string? action)
    {
        return string.IsNullOrWhiteSpace(action)
            ? string.Empty
            : action.Trim().ToLowerInvariant();
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private static bool IsSupportedMemoryAction(string action)
    {
        return action is "remember" or "forget" or "update";
    }

    [GeneratedRegex("<aim-management>\\s*(?<json>\\{.*?\\})\\s*</aim-management>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ManagementBlockRegex();

    private sealed record DirectiveDto(MemoryDirectiveDto[]? Memories, PersonalityDirectiveDto? Personality);

    private sealed record MemoryDirectiveDto(string? Action, string? Content, string? OldContent);

    private sealed record PersonalityDirectiveDto(string? Status, string? SystemPrompt, string? SystemPromptAppend);
}
