using AIM.Core.Chat;
using AIM.Providers.Fakes;
using AIM.Providers.InMemory;

namespace AIM.Tests;

public sealed class FakeProviderTests
{
    [Fact]
    public async Task FakeProviderStreamsResponseForSeededPersonality()
    {
        var personalityService = new InMemoryPersonalityService();
        var conversationService = new InMemoryConversationService();
        var provider = new FakeAiProvider();

        var personality = (await personalityService.ListAsync()).First();
        var conversation = await conversationService.GetOrCreateConversationAsync(personality.Id);
        await conversationService.AddMessageAsync(conversation.Id, ChatRole.User, "Can you hear me?");
        var messages = await conversationService.GetMessagesAsync(conversation.Id);

        var chunks = new List<string>();

        await foreach (var chunk in provider.StreamChatAsync(new ChatRequest(personality, conversation, messages, ChatContext.Empty)))
        {
            chunks.Add(chunk.Delta);
        }

        var response = string.Concat(chunks);

        Assert.Contains(personality.DisplayName, response);
        Assert.Contains("Can you hear me?", response);
    }
}
