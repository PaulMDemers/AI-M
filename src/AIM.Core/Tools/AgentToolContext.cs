using AIM.Core.Chat;
using AIM.Core.Personalities;

namespace AIM.Core.Tools;

public sealed record AgentToolContext(
    Personality Personality,
    Conversation Conversation);
