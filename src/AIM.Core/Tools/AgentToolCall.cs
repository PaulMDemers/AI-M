using System.Text.Json.Nodes;

namespace AIM.Core.Tools;

public sealed record AgentToolCall(
    string Id,
    string Name,
    JsonObject Arguments);
