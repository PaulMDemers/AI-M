using AIM.Core.Personalities;

namespace AIM.Core.Chat;

public sealed record ChatRequest(
    Personality Personality,
    Conversation Conversation,
    IReadOnlyList<ChatMessage> Messages,
    ChatContext Context);
