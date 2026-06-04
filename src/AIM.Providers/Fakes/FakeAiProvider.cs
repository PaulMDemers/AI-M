using System.Runtime.CompilerServices;
using AIM.Core.Chat;
using AIM.Core.Providers;

namespace AIM.Providers.Fakes;

public sealed class FakeAiProvider : IAiProvider
{
    public string Key => "fake";

    public string DisplayName => "Local Fake Provider";

    public async IAsyncEnumerable<ChatStreamChunk> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var lastUserMessage = request.Messages.LastOrDefault(message => message.Role == ChatRole.User);
        var userText = lastUserMessage?.Content ?? "hello";

        var response =
            $"{request.Personality.DisplayName}: I heard \"{userText}\". " +
            "This is the local preview provider streaming through the same contract the real providers will use. " +
            $"My memory set is {request.Personality.MemorySetId:N}. " +
            $"I received {request.Context.Memories.Count} approved memories and " +
            $"{(request.Context.HasSelfManagementInstructions ? "can" : "cannot")} self-manage context.";

        foreach (var word in response.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(45, cancellationToken);
            yield return new ChatStreamChunk(word + " ");
        }

        yield return new ChatStreamChunk(string.Empty, IsFinal: true);
    }
}
