using Microsoft.Extensions.Configuration;

namespace AIM.Providers.Ollama;

public sealed class OllamaProviderSettings
{
    public const string SectionName = "AIM:Providers:Ollama";

    public Uri Endpoint { get; init; } = new("http://localhost:11434");

    public string? ModelId { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ModelId);

    public static OllamaProviderSettings FromConfiguration(IConfiguration? configuration)
    {
        var section = configuration?.GetSection(SectionName);
        var endpointText =
            section?["Endpoint"] ??
            configuration?["Ollama:Endpoint"] ??
            Environment.GetEnvironmentVariable("AIM_OLLAMA_ENDPOINT") ??
            "http://localhost:11434";
        var modelId =
            section?["ModelId"] ??
            configuration?["Ollama:ModelId"] ??
            Environment.GetEnvironmentVariable("AIM_OLLAMA_MODEL");

        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint))
        {
            endpoint = new Uri("http://localhost:11434");
        }

        return new OllamaProviderSettings
        {
            Endpoint = endpoint,
            ModelId = string.IsNullOrWhiteSpace(modelId) ? null : modelId.Trim()
        };
    }
}
