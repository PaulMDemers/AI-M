using Microsoft.Extensions.Configuration;

namespace AIM.Providers.OpenAI;

public sealed class OpenAiProviderSettings
{
    public const string SectionName = "AIM:Providers:OpenAI";

    public string? ApiKey { get; init; }

    public string ModelId { get; init; } = "gpt-4.1-mini";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public static OpenAiProviderSettings FromConfiguration(IConfiguration? configuration)
    {
        var section = configuration?.GetSection(SectionName);

        var apiKey =
            section?["ApiKey"] ??
            configuration?["OpenAI:ApiKey"] ??
            Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        var modelId =
            section?["ModelId"] ??
            configuration?["OpenAI:ModelId"] ??
            Environment.GetEnvironmentVariable("AIM_OPENAI_MODEL") ??
            "gpt-4.1-mini";

        return new OpenAiProviderSettings
        {
            ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim(),
            ModelId = string.IsNullOrWhiteSpace(modelId) ? "gpt-4.1-mini" : modelId.Trim()
        };
    }
}
