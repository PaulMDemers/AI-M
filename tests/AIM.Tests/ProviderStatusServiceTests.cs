using System.Runtime.CompilerServices;
using AIM.Core.Chat;
using AIM.Core.Providers;
using AIM.Core.Services;
using AIM.Providers;

namespace AIM.Tests;

public sealed class ProviderStatusServiceTests
{
    [Fact]
    public async Task RefreshAllCachesAndPublishesProviderStatus()
    {
        var account = Account("openai", "openai", credential: "key", modelId: "gpt-4.1-mini");
        var service = new ProviderStatusService(
            new FakeProviderAccountService([account]),
            new FakeDiagnosticsService(),
            [new FakeProvider("openai")]);
        var changed = new List<string>();
        service.ProviderStatusChanged += (_, args) => changed.Add(args.ProviderKey);

        var snapshot = await service.RefreshAllAsync();

        Assert.True(snapshot.ContainsKey("openai"));
        Assert.NotNull(service.GetCached("openai"));
        Assert.Contains("openai", changed);
    }

    [Fact]
    public async Task ClearRemovesCachedProviderAndPublishesNullStatus()
    {
        var account = Account("openai", "openai", credential: "key", modelId: "gpt-4.1-mini");
        var service = new ProviderStatusService(
            new FakeProviderAccountService([account]),
            new FakeDiagnosticsService(),
            [new FakeProvider("openai")]);
        ProviderStatusChangedEventArgs? lastChange = null;
        service.ProviderStatusChanged += (_, args) => lastChange = args;

        await service.RefreshAllAsync();
        service.Clear("openai");

        Assert.Null(service.GetCached("openai"));
        Assert.Equal("openai", lastChange?.ProviderKey);
        Assert.Null(lastChange?.Diagnostic);
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

    private sealed class FakeProviderAccountService : IProviderAccountService
    {
        private readonly IReadOnlyDictionary<string, ProviderAccount> _accounts;

        public FakeProviderAccountService(IEnumerable<ProviderAccount> accounts)
        {
            _accounts = accounts.ToDictionary(account => account.Key, StringComparer.OrdinalIgnoreCase);
        }

        public Task<ProviderAccount?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            _accounts.TryGetValue(key, out var account);
            return Task.FromResult(account);
        }

        public Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProviderAccount>>(_accounts.Values.ToArray());
        }

        public Task SaveAsync(
            string key,
            string displayName,
            string providerKind,
            string? endpoint,
            string? defaultModelId,
            string? credential,
            bool isEnabled,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeDiagnosticsService : IProviderDiagnosticsService
    {
        public Task<ProviderDiagnosticResult> CheckAsync(
            ProviderAccount account,
            bool providerRegistered,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderDiagnosticResult(
                account.Key,
                ProviderDiagnosticState.Ready,
                "Verified",
                $"{account.DisplayName} is verified.",
                IsConfigured: true,
                IsVerified: true));
        }
    }

    private sealed class FakeProvider : IAiProvider
    {
        public FakeProvider(string key)
        {
            Key = key;
        }

        public string Key { get; }

        public string DisplayName => Key;

        public async IAsyncEnumerable<ChatStreamChunk> StreamChatAsync(
            ChatRequest request,
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
