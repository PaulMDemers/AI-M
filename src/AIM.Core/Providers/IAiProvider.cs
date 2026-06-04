using AIM.Core.Chat;

namespace AIM.Core.Providers;

public interface IAiProvider
{
    string Key { get; }

    string DisplayName { get; }

    bool SupportsNativeTools => false;

    IAsyncEnumerable<ChatStreamChunk> StreamChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default);
}
