using Microsoft.Extensions.Configuration;

namespace AIM.Providers.Bedrock;

public sealed class BedrockProviderSettings
{
    public const string SectionName = "AIM:Providers:Bedrock";

    public string Region { get; init; } = "us-east-1";

    public string? ModelId { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ModelId);

    public static BedrockProviderSettings FromConfiguration(IConfiguration? configuration)
    {
        var section = configuration?.GetSection(SectionName);
        var region =
            section?["Region"] ??
            configuration?["AWS:Region"] ??
            Environment.GetEnvironmentVariable("AWS_REGION") ??
            Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ??
            "us-east-1";
        var modelId =
            section?["ModelId"] ??
            configuration?["Bedrock:ModelId"] ??
            Environment.GetEnvironmentVariable("AIM_BEDROCK_MODEL");

        return new BedrockProviderSettings
        {
            Region = string.IsNullOrWhiteSpace(region) ? "us-east-1" : region.Trim(),
            ModelId = string.IsNullOrWhiteSpace(modelId) ? null : modelId.Trim()
        };
    }
}
