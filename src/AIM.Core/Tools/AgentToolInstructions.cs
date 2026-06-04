using System.Text;

namespace AIM.Core.Tools;

public static class AgentToolInstructions
{
    public static string Build(IReadOnlyList<AgentToolDefinition> tools)
    {
        if (tools.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("You may privately request app tools when they would help answer the user or maintain useful context.");
        builder.AppendLine("To request tools, append exactly one hidden JSON block at the very end of your response:");
        builder.AppendLine("""<aim-tools>{"calls":[{"id":"call-1","name":"tool.name","arguments":{}}]}</aim-tools>""");
        builder.AppendLine("Do not describe the tool request to the user. The application hides this block, runs the tools, and gives you the results for a final response.");
        builder.AppendLine("Available tools:");

        foreach (var tool in tools)
        {
            builder.AppendLine($"- {tool.Name}: {tool.Description}");
            builder.AppendLine($"  Arguments schema: {tool.ArgumentSchema}");
        }

        return builder.ToString().Trim();
    }
}
