namespace AIM.Core.Chat;

public sealed record ChatStreamChunk(string Delta, bool IsFinal = false);
