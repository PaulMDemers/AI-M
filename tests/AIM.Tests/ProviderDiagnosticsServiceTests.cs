using System.Net;
using AIM.Core.Providers;
using AIM.Providers;

namespace AIM.Tests;

public sealed class ProviderDiagnosticsServiceTests
{
    [Fact]
    public async Task OpenAiReportsVerifiedModel()
    {
        using var service = new ProviderDiagnosticsService(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK))));

        var result = await service.CheckAsync(
            Account("openai", "openai", modelId: "gpt-4.1-mini", credential: "key"),
            providerRegistered: true);

        Assert.Equal(ProviderDiagnosticState.Ready, result.State);
        Assert.True(result.IsVerified);
    }

    [Fact]
    public async Task OpenAiReportsRejectedApiKey()
    {
        using var service = new ProviderDiagnosticsService(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized))));

        var result = await service.CheckAsync(
            Account("openai", "openai", modelId: "gpt-4.1-mini", credential: "key"),
            providerRegistered: true);

        Assert.Equal(ProviderDiagnosticState.Unauthorized, result.State);
        Assert.True(result.IsConfigured);
        Assert.False(result.IsVerified);
    }

    [Fact]
    public async Task OllamaReportsInstalledAndMissingModels()
    {
        using var service = new ProviderDiagnosticsService(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"models":[{"name":"llama3.2:latest"}]}""")
            })));

        var ready = await service.CheckAsync(
            Account("ollama", "ollama", endpoint: "http://localhost:11434", modelId: "llama3.2"),
            providerRegistered: true);
        var missing = await service.CheckAsync(
            Account("ollama", "ollama", endpoint: "http://localhost:11434", modelId: "qwen3"),
            providerRegistered: true);

        Assert.Equal(ProviderDiagnosticState.Ready, ready.State);
        Assert.Equal(ProviderDiagnosticState.SetupNeeded, missing.State);
        Assert.True(missing.IsVerified);
    }

    [Fact]
    public async Task BedrockReportsConfiguredButUnverified()
    {
        using var service = new ProviderDiagnosticsService(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK))));

        var result = await service.CheckAsync(
            Account("bedrock", "bedrock", endpoint: "us-east-1", modelId: "amazon.nova-pro-v1:0"),
            providerRegistered: true);

        Assert.Equal(ProviderDiagnosticState.Configured, result.State);
        Assert.True(result.IsUsable);
        Assert.False(result.IsVerified);
    }

    private static ProviderAccount Account(
        string key,
        string kind,
        string? endpoint = null,
        string? credential = null,
        string? modelId = null,
        bool isEnabled = true)
    {
        return new ProviderAccount(
            Guid.NewGuid(),
            key,
            key,
            kind,
            endpoint,
            modelId,
            credential,
            isEnabled);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_respond(request));
        }
    }
}
