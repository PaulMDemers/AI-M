using System.Text.Json;
using AIM.Core.Tools;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class ToolTraceViewModel
{
    private ToolTraceViewModel(
        string id,
        string name,
        string arguments,
        bool success,
        string result,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Arguments = arguments;
        Success = success;
        Result = result;
        CreatedAt = createdAt;
    }

    public string Id { get; }

    public string Name { get; }

    public string Arguments { get; }

    public bool Success { get; }

    public string Result { get; }

    public DateTimeOffset CreatedAt { get; }

    public string CreatedAtLabel => CreatedAt.ToString("t");

    public string StatusLabel => Success ? "ok" : "failed";

    public string Header => $"{CreatedAtLabel}  {Name}  {StatusLabel}";

    public static ToolTraceViewModel From(AgentToolCall call, AgentToolResult result)
    {
        var arguments = JsonSerializer.Serialize(call.Arguments);

        return new ToolTraceViewModel(
            call.Id,
            call.Name,
            arguments,
            result.Success,
            result.Content,
            DateTimeOffset.Now);
    }
}
