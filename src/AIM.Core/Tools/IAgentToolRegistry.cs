namespace AIM.Core.Tools;

public interface IAgentToolRegistry
{
    IReadOnlyList<AgentToolDefinition> ListTools();

    Task<AgentToolResult> ExecuteAsync(
        AgentToolCall call,
        AgentToolContext context,
        CancellationToken cancellationToken = default);
}
