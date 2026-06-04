using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIM.Core.Chat;
using AIM.Core.Providers;
using AIM.Core.Services;
using AIM.Core.Tools;
using OpenAI.Chat;
using AimChatMessage = AIM.Core.Chat.ChatMessage;
using OpenAiChatMessage = OpenAI.Chat.ChatMessage;

namespace AIM.Providers.OpenAI;

public sealed class OpenAiProvider : IAiProvider
{
    public const string ProviderKey = "openai";

    private readonly IProviderAccountService? _providerAccountService;
    private readonly string? _apiKey;

    public OpenAiProvider(string apiKey, string modelId)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("An OpenAI API key is required.", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("An OpenAI model id is required.", nameof(modelId));
        }

        ModelId = modelId;
        _apiKey = apiKey;
    }

    public OpenAiProvider(IProviderAccountService providerAccountService, OpenAiProviderSettings fallbackSettings)
    {
        _providerAccountService = providerAccountService;
        ModelId = fallbackSettings.ModelId;
        _apiKey = fallbackSettings.ApiKey;
    }

    public string Key => ProviderKey;

    public string DisplayName => "OpenAI";

    public bool SupportsNativeTools => true;

    public string ModelId { get; }

    public async IAsyncEnumerable<ChatStreamChunk> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var account = _providerAccountService is null
            ? null
            : await _providerAccountService.GetAsync(ProviderKey, cancellationToken);
        var apiKey = account?.Credential ?? _apiKey;
        var modelId = account?.DefaultModelId ?? request.Personality.ModelId ?? ModelId;

        if (account?.IsEnabled == false || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI is not configured. Add an API key in Provider Settings or set OPENAI_API_KEY.");
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new InvalidOperationException("OpenAI model is not configured.");
        }

        var chatClient = new ChatClient(modelId, apiKey);
        var messages = ToOpenAiMessages(request);
        var options = CreateOptions(request);
        var toolCallBuilders = new Dictionary<int, StreamingToolCallBuilder>();
        var updates = chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken);

        await foreach (var update in updates.WithCancellation(cancellationToken))
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    yield return new ChatStreamChunk(contentPart.Text);
                }
            }

            foreach (var toolCallUpdate in update.ToolCallUpdates)
            {
                var builder = GetOrCreateToolCallBuilder(toolCallBuilders, toolCallUpdate.Index);
                builder.Apply(toolCallUpdate);
            }
        }

        if (toolCallBuilders.Count > 0)
        {
            yield return new ChatStreamChunk(BuildToolRequestBlock(toolCallBuilders.Values, request.Context.ToolDefinitions));
        }

        yield return new ChatStreamChunk(string.Empty, IsFinal: true);
    }

    private static ChatCompletionOptions CreateOptions(ChatRequest request)
    {
        var options = new ChatCompletionOptions();

        foreach (var tool in request.Context.ToolDefinitions)
        {
            options.Tools.Add(ChatTool.CreateFunctionTool(
                ToOpenAiToolName(tool.Name),
                tool.Description,
                BinaryData.FromString(tool.ArgumentSchema),
                functionSchemaIsStrict: false));
        }

        if (request.Context.HasToolDefinitions)
        {
            options.AllowParallelToolCalls = true;
        }

        return options;
    }

    private static List<OpenAiChatMessage> ToOpenAiMessages(ChatRequest request)
    {
        var messages = new List<OpenAiChatMessage>
        {
            new SystemChatMessage(request.Personality.SystemPrompt)
        };

        if (request.Context.HasSystemContext)
        {
            messages.Add(new SystemChatMessage(BuildContextMessage(request)));
        }

        foreach (var message in request.Messages)
        {
            var openAiMessage = ToOpenAiMessage(message);

            if (openAiMessage is not null)
            {
                messages.Add(openAiMessage);
            }
        }

        return messages;
    }

    private static string BuildContextMessage(ChatRequest request)
    {
        var sections = new List<string>();

        if (request.Context.HasSelfManagementInstructions)
        {
            sections.Add(request.Context.SelfManagementInstructions);
        }

        if (request.Context.HasToolInstructions && !request.Context.HasToolDefinitions)
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

    private static OpenAiChatMessage? ToOpenAiMessage(AimChatMessage message)
    {
        return message.Role switch
        {
            ChatRole.System => new SystemChatMessage(message.Content),
            ChatRole.Tool => new SystemChatMessage($"Tool results:\n{message.Content}"),
            ChatRole.User => new UserChatMessage(message.Content),
            ChatRole.Assistant => new AssistantChatMessage(message.Content),
            _ => null
        };
    }

    private static StreamingToolCallBuilder GetOrCreateToolCallBuilder(
        Dictionary<int, StreamingToolCallBuilder> builders,
        int index)
    {
        if (!builders.TryGetValue(index, out var builder))
        {
            builder = new StreamingToolCallBuilder();
            builders[index] = builder;
        }

        return builder;
    }

    private static string BuildToolRequestBlock(
        IEnumerable<StreamingToolCallBuilder> toolCalls,
        IReadOnlyList<AgentToolDefinition> toolDefinitions)
    {
        var calls = toolCalls
            .Select(toolCall => new
            {
                id = string.IsNullOrWhiteSpace(toolCall.Id) ? Guid.NewGuid().ToString("N") : toolCall.Id,
                name = ToAimToolName(toolCall.Name, toolDefinitions),
                arguments = ParseArguments(toolCall.Arguments)
            })
            .Where(call => !string.IsNullOrWhiteSpace(call.name))
            .ToArray();

        if (calls.Length == 0)
        {
            return string.Empty;
        }

        var json = JsonSerializer.Serialize(new { calls });
        return $"<aim-tools>{json}</aim-tools>";
    }

    private static string ToOpenAiToolName(string toolName)
    {
        var builder = new StringBuilder(toolName.Length);

        foreach (var character in toolName)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-'
                ? character
                : '_');
        }

        return builder.ToString();
    }

    private static string ToAimToolName(string? openAiToolName, IReadOnlyList<AgentToolDefinition> toolDefinitions)
    {
        if (string.IsNullOrWhiteSpace(openAiToolName))
        {
            return string.Empty;
        }

        var match = toolDefinitions.FirstOrDefault(tool =>
            string.Equals(ToOpenAiToolName(tool.Name), openAiToolName, StringComparison.OrdinalIgnoreCase));

        return match?.Name ?? openAiToolName;
    }

    private static JsonObject ParseArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(arguments) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class StreamingToolCallBuilder
    {
        private readonly StringBuilder _arguments = new();

        public string? Id { get; private set; }

        public string? Name { get; private set; }

        public string Arguments => _arguments.ToString();

        public void Apply(StreamingChatToolCallUpdate update)
        {
            if (!string.IsNullOrWhiteSpace(update.ToolCallId))
            {
                Id = update.ToolCallId;
            }

            if (!string.IsNullOrWhiteSpace(update.FunctionName))
            {
                Name = update.FunctionName;
            }

            if (update.FunctionArgumentsUpdate is not null)
            {
                _arguments.Append(update.FunctionArgumentsUpdate);
            }
        }
    }
}
