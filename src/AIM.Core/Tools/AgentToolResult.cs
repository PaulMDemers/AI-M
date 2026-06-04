namespace AIM.Core.Tools;

public sealed record AgentToolResult(
    string Id,
    string Name,
    bool Success,
    string Content);
