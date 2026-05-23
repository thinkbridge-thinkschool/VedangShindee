using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using QuotesApi.Services;
using Xunit;

namespace Quotes.Tests.Unit;

public sealed class EntraDiscoveryServiceTests
{
    private const string ValidDiscoveryJson = """
        {
            "jwks_uri": "https://login.microsoftonline.com/common/discovery/v2.0/keys",
            "issuer": "https://login.microsoftonline.com/{tenantid}/v2.0"
        }
        """;

    /// <summary>
    /// Simulates a flaky upstream: two 503 responses followed by a 200.
    /// The retry pipeline should transparently recover and return the JWKS URI.
    /// Retry warnings must appear in the log — one per failed attempt.
    /// </summary>
    [Fact]
    public async Task GetJwksUriAsync_RetriesOnTransient503_LogsEachRetryAndSucceeds()
    {
        // --- Arrange ---
        var callCount = 0;
        var fakeHandler = new FakeHttpMessageHandler(_ =>
        {
            callCount++;
            if (callCount <= 2)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidDiscoveryJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var fakeLogger = new FakeLogger<EntraDiscoveryService>();

        var services = new ServiceCollection();

        // Register our capture logger before anything else so it wins over the open-generic
        // ILogger<T> that AddLogging() registers via TryAdd.
        services.AddSingleton<ILogger<EntraDiscoveryService>>(fakeLogger);

        services.AddHttpClient<IEntraDiscoveryService, EntraDiscoveryService>()
            .ConfigurePrimaryHttpMessageHandler(() => fakeHandler)
            .AddResilienceHandler("default", (builder, ctx) =>
            {
                var logger = ctx.ServiceProvider
                    .GetRequiredService<ILogger<EntraDiscoveryService>>();

                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = false,
                    Delay = TimeSpan.FromMilliseconds(1), // near-zero delay keeps tests fast
                    OnRetry = args =>
                    {
                        var reason = args.Outcome.Exception?.Message
                            ?? $"HTTP {(int)args.Outcome.Result!.StatusCode}";
                        logger.LogWarning(
                            "[Polly] Retry {Attempt} — {Reason}",
                            args.AttemptNumber + 1, reason);
                        return ValueTask.CompletedTask;
                    }
                });

                builder.AddTimeout(TimeSpan.FromSeconds(10));
            });

        await using var provider = services.BuildServiceProvider();
        var svc = provider.GetRequiredService<IEntraDiscoveryService>();

        // --- Act ---
        var result = await svc.GetJwksUriAsync("test-tenant-id");

        // --- Assert ---
        result.Should().Be("https://login.microsoftonline.com/common/discovery/v2.0/keys");
        callCount.Should().Be(3, "two 503s and then one 200");

        var warnings = fakeLogger.Logs.Where(e => e.Level == LogLevel.Warning).ToList();
        warnings.Should().HaveCount(2, "one warning logged per retry attempt");
        warnings[0].Message.Should().Contain("Retry 1");
        warnings[1].Message.Should().Contain("Retry 2");
    }

    [Fact]
    public async Task GetJwksUriAsync_ExhaustsRetries_ThrowsAfterMaxAttempts()
    {
        var fakeHandler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<EntraDiscoveryService>>(new FakeLogger<EntraDiscoveryService>());

        services.AddHttpClient<IEntraDiscoveryService, EntraDiscoveryService>()
            .ConfigurePrimaryHttpMessageHandler(() => fakeHandler)
            .AddResilienceHandler("default", (builder, _) =>
            {
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(1),
                    UseJitter = false
                });
                builder.AddTimeout(TimeSpan.FromSeconds(10));
            });

        await using var provider = services.BuildServiceProvider();
        var svc = provider.GetRequiredService<IEntraDiscoveryService>();

        await svc.Invoking(s => s.GetJwksUriAsync("bad-tenant"))
            .Should().ThrowAsync<HttpRequestException>();
    }
}

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => _respond = respond;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_respond(request));
}

public sealed record LogEntry(LogLevel Level, string Message);

public sealed class FakeLogger<T> : ILogger<T>
{
    public List<LogEntry> Logs { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
        => Logs.Add(new LogEntry(logLevel, formatter(state, exception)));
}
