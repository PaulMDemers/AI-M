namespace AIM.Core.Tools;

public sealed record AgentToolDefinition(
    string Name,
    string Description,
    string ArgumentSchema,
    bool RequiresApproval = false);
