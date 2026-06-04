using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AIM.Core.Tools;

public sealed record AgentToolRequest(IReadOnlyList<AgentToolCall> Calls)
{
    public static AgentToolRequest Empty { get; } = new([]);

    public bool HasCalls => Calls.Count > 0;
}

public sealed record AgentToolRequestExtraction(
    string VisibleContent,
    AgentToolRequest Request);

public static partial class AgentToolRequestParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static AgentToolRequestExtraction Extract(string content)
    {
        var match = ToolBlockRegex().Match(content);

        if (!match.Success)
        {
            return new AgentToolRequestExtraction(content, AgentToolRequest.Empty);
        }

        var visibleContent = ToolBlockRegex().Replace(content, string.Empty).TrimEnd();

        try
        {
            var dto = JsonSerializer.Deserialize<ToolRequestDto>(match.Groups["json"].Value, SerializerOptions);
            var calls = dto?.Calls?
                .Select(Map)
                .Where(call => !string.IsNullOrWhiteSpace(call.Name))
                .ToArray() ?? [];

            return new AgentToolRequestExtraction(visibleContent, new AgentToolRequest(calls));
        }
        catch (JsonException)
        {
            return new AgentToolRequestExtraction(visibleContent, AgentToolRequest.Empty);
        }
    }

    private static AgentToolCall Map(ToolCallDto dto)
    {
        return new AgentToolCall(
            string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString("N") : dto.Id.Trim(),
            dto.Name?.Trim() ?? string.Empty,
            dto.Arguments ?? []);
    }

    [GeneratedRegex("<aim-tools>\\s*(?<json>\\{.*?\\})\\s*</aim-tools>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ToolBlockRegex();

    private sealed record ToolRequestDto(ToolCallDto[]? Calls);

    private sealed record ToolCallDto(string? Id, string? Name, JsonObject? Arguments);
}
