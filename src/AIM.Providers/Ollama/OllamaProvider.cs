using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AIM.Core.Chat;
using AIM.Core.Providers;
using AIM.Core.Services;

namespace AIM.Providers.Ollama;

public sealed class OllamaProvider : IAiProvider, IDisposable
{
    public const string ProviderKey = "ollama";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IProviderAccountService? _providerAccountService;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public OllamaProvider(OllamaProviderSettings settings)
        : this(settings, new HttpClient(), ownsHttpClient: true)
    {
    }

    public OllamaProvider(OllamaProviderSettings settings, HttpClient httpClient)
        : this(settings, httpClient, ownsHttpClient: false)
    {
    }

    private OllamaProvider(OllamaProviderSettings settings, HttpClient httpClient, bool ownsHttpClient)
    {
        Settings = settings;
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
    }

    public OllamaProvider(
        OllamaProviderSettings settings,
        IProviderAccountService providerAccountService,
        HttpClient? httpClient = null)
        : this(settings, httpClient ?? new HttpClient(), ownsHttpClient: httpClient is null)
    {
        _providerAccountService = providerAccountService;
    }

    public string Key => ProviderKey;

    public string DisplayName => "Ollama";

    public OllamaProviderSettings Settings { get; }

    public async IAsyncEnumerable<ChatStreamChunk> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var account = _providerAccountService is null
            ? null
            : await _providerAccountService.GetAsync(ProviderKey, cancellationToken);
        var endpoint = GetEndpoint(account);
        var modelId = account?.DefaultModelId ?? Settings.ModelId;

        if (account?.IsEnabled == false || string.IsNullOrWhiteSpace(modelId))
        {
            throw new InvalidOperationException("Ollama is not configured. Set a model in Provider Settings or AIM_OLLAMA_MODEL.");
        }

        var uri = new Uri(endpoint, "/api/chat");
        var payload = new OllamaChatRequest(modelId, ToOllamaMessages(request), Stream: true);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Ollama returned {(int)response.StatusCode} {response.ReasonPhrase}: {error}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, SerializerOptions);

            if (!string.IsNullOrEmpty(chunk?.Message?.Content))
            {
                yield return new ChatStreamChunk(chunk.Message.Content);
            }

            if (chunk?.Done == true)
            {
                yield return new ChatStreamChunk(string.Empty, IsFinal: true);
                yield break;
            }
        }

        yield return new ChatStreamChunk(string.Empty, IsFinal: true);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static IReadOnlyList<OllamaChatMessage> ToOllamaMessages(ChatRequest request)
    {
        var messages = new List<OllamaChatMessage>
        {
            new("system", request.Personality.SystemPrompt)
        };

        if (request.Context.HasSystemContext)
        {
            messages.Add(new OllamaChatMessage("system", BuildContextMessage(request)));
        }

        foreach (var message in request.Messages)
        {
            var role = message.Role switch
            {
                ChatRole.System => "system",
                ChatRole.Tool => "system",
                ChatRole.User => "user",
                ChatRole.Assistant => "assistant",
                _ => null
            };

            if (role is not null)
            {
                messages.Add(new OllamaChatMessage(role, message.Content));
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

    private Uri GetEndpoint(ProviderAccount? account)
    {
        if (account is not null &&
            !string.IsNullOrWhiteSpace(account.Endpoint) &&
            Uri.TryCreate(account.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return endpoint;
        }

        return Settings.Endpoint;
    }

    private sealed record OllamaChatRequest(
        string Model,
        IReadOnlyList<OllamaChatMessage> Messages,
        bool Stream);

    private sealed record OllamaChatMessage(string Role, string Content);

    private sealed record OllamaChatResponse(OllamaChatResponseMessage? Message, bool Done);

    private sealed record OllamaChatResponseMessage(string? Role, string? Content, string? Thinking);
}
