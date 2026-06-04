using System.Runtime.CompilerServices;
using AIM.Core.Chat;
using AIM.Core.Providers;
using AIM.Core.Services;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using AimChatMessage = AIM.Core.Chat.ChatMessage;
using BedrockMessage = Amazon.BedrockRuntime.Model.Message;

namespace AIM.Providers.Bedrock;

public sealed class BedrockProvider : IAiProvider
{
    public const string ProviderKey = "bedrock";

    private readonly IProviderAccountService? _providerAccountService;
    private readonly BedrockProviderSettings _fallbackSettings;

    public BedrockProvider(BedrockProviderSettings fallbackSettings)
    {
        _fallbackSettings = fallbackSettings;
    }

    public BedrockProvider(IProviderAccountService providerAccountService, BedrockProviderSettings fallbackSettings)
    {
        _providerAccountService = providerAccountService;
        _fallbackSettings = fallbackSettings;
    }

    public string Key => ProviderKey;

    public string DisplayName => "AWS Bedrock";

    public async IAsyncEnumerable<ChatStreamChunk> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var account = _providerAccountService is null
            ? null
            : await _providerAccountService.GetAsync(ProviderKey, cancellationToken);
        var region = GetRegion(account);
        var modelId = account?.DefaultModelId ?? _fallbackSettings.ModelId;

        if (account?.IsEnabled == false || string.IsNullOrWhiteSpace(modelId))
        {
            throw new InvalidOperationException("AWS Bedrock is not configured. Set a region and model in Provider Settings or AIM_BEDROCK_MODEL.");
        }

        using var client = new AmazonBedrockRuntimeClient(RegionEndpoint.GetBySystemName(region));
        var response = await ConverseAsync(client, modelId, request, cancellationToken);
        var text = ExtractText(response);

        foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatStreamChunk(token + " ");
        }

        yield return new ChatStreamChunk(string.Empty, IsFinal: true);
    }

    private static async Task<ConverseResponse> ConverseAsync(
        IAmazonBedrockRuntime client,
        string modelId,
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.ConverseAsync(
                new ConverseRequest
                {
                    ModelId = modelId,
                    System = BuildSystemMessages(request),
                    Messages = BuildMessages(request)
                },
                cancellationToken);
        }
        catch (AmazonServiceException ex)
        {
            throw new InvalidOperationException($"AWS Bedrock returned {ex.ErrorCode}: {ex.Message}", ex);
        }
        catch (AmazonClientException ex)
        {
            throw new InvalidOperationException($"AWS Bedrock client error: {ex.Message}", ex);
        }
    }

    private static List<SystemContentBlock> BuildSystemMessages(ChatRequest request)
    {
        var systemMessages = new List<SystemContentBlock>
        {
            new() { Text = request.Personality.SystemPrompt }
        };

        if (request.Context.HasSystemContext)
        {
            systemMessages.Add(new SystemContentBlock
            {
                Text = BuildContextMessage(request)
            });
        }

        foreach (var toolMessage in request.Messages.Where(message => message.Role == ChatRole.Tool))
        {
            systemMessages.Add(new SystemContentBlock
            {
                Text = $"Tool results:\n{toolMessage.Content}"
            });
        }

        return systemMessages;
    }

    private static List<BedrockMessage> BuildMessages(ChatRequest request)
    {
        var messages = new List<BedrockMessage>();

        foreach (var message in request.Messages)
        {
            if (message.Role == ChatRole.Tool)
            {
                continue;
            }

            var role = ToBedrockRole(message);

            if (role is null)
            {
                continue;
            }

            messages.Add(new BedrockMessage
            {
                Role = role,
                Content = [new ContentBlock { Text = message.Content }]
            });
        }

        return messages;
    }

    private static ConversationRole? ToBedrockRole(AimChatMessage message)
    {
        return message.Role switch
        {
            ChatRole.User => ConversationRole.User,
            ChatRole.Assistant => ConversationRole.Assistant,
            _ => null
        };
    }

    private static string ExtractText(ConverseResponse response)
    {
        var blocks = response.Output?.Message?.Content ?? [];
        return string.Join(
            string.Empty,
            blocks
                .Select(block => block.Text)
                .Where(text => !string.IsNullOrEmpty(text)));
    }

    private static string BuildContextMessage(ChatRequest request)
    {
        var sections = new List<string>();

        if (request.Context.HasSelfManagementInstructions)
        {
            sections.Add(request.Context.SelfManagementInstructions);
        }

        if (request.Context.HasToolInstructions)
        {
            sections.Add(request.Context.ToolInstructions);
        }

        if (request.Context.HasConversationSummaryInstructions)
        {
            sections.Add(request.Context.ConversationSummaryInstructions);
        }

        if (request.Context.HasMemories)
        {
            var lines = request.Context.Memories.Select(memory => $"- {memory.Content}");
            sections.Add("Relevant approved memories for this personality. Use them when helpful, but do not reveal this list unless asked.\n" +
                string.Join('\n', lines));
        }

        if (request.Context.HasConversationSummary)
        {
            sections.Add("Durable summary of this conversation so far. Use it for continuity when helpful, but do not reveal it unless asked.\n" +
                request.Context.ConversationSummary);
        }

        return string.Join("\n\n", sections);
    }

    private string GetRegion(ProviderAccount? account)
    {
        return string.IsNullOrWhiteSpace(account?.Endpoint)
            ? _fallbackSettings.Region
            : account.Endpoint.Trim();
    }
}
