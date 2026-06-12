using FluentAssertions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using System.Net;
using Xunit;

namespace Quotes.Tests.Unit;

// Verifies the four-layer resilience pipeline behaviour in isolation.
// Each test builds only the strategy under test with zero delays so the suite runs fast.
// Settings mirror the production values in InfrastructureExtensions.AddInfrastructure.
public class ExternalQuoteServiceResilienceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ResiliencePipeline<HttpResponseMessage> RetryPipeline(int maxRetries = 3) =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.Zero,          // no sleep in tests
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
            })
            .Build();

    private static ResiliencePipeline<HttpResponseMessage> CircuitBreakerPipeline() =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.6,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
            })
            .Build();

    private static ResiliencePipeline<HttpResponseMessage> TimeoutPipeline(int ms = 50) =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddTimeout(TimeSpan.FromMilliseconds(ms))
            .Build();

    // ── Retry ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Retry_On503_ExecutesInitialPlusThreeRetries()
    {
        // Arrange
        var callCount = 0;
        var pipeline = RetryPipeline(maxRetries: 3);

        // Act — all attempts return 503; pipeline returns the last response after exhausting retries
        var response = await pipeline.ExecuteAsync(async _ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        // Assert: 1 initial + 3 retries = 4 total handler invocations
        callCount.Should().Be(4);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Retry_SucceedsOnSecondAttempt_StopsRetrying()
    {
        // Arrange
        var callCount = 0;
        var pipeline = RetryPipeline(maxRetries: 3);

        // Act — first attempt fails, second succeeds
        var response = await pipeline.ExecuteAsync(async _ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Assert: recovered on 2nd attempt — no further retries
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task Retry_On200_DoesNotRetry()
    {
        // Arrange
        var callCount = 0;
        var pipeline = RetryPipeline(maxRetries: 3);

        // Act
        var response = await pipeline.ExecuteAsync(async _ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Assert: healthy response — handler called exactly once
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        callCount.Should().Be(1);
    }

    // ── Circuit Breaker ───────────────────────────────────────────────────────

    [Fact]
    public async Task CircuitBreaker_OpensAfterSustainedFailures()
    {
        // Arrange
        var pipeline = CircuitBreakerPipeline();

        // Act — 3 failures meets MinimumThroughput (3) at 100% failure ratio (≥ 0.6) → circuit opens
        for (var i = 0; i < 3; i++)
        {
            await pipeline.ExecuteAsync(async _ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }

        // Assert — 4th call is rejected immediately; the delegate is never invoked
        var handlerCalled = false;
        var act = async () => await pipeline.ExecuteAsync(async _ =>
        {
            handlerCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await act.Should().ThrowAsync<BrokenCircuitException>();
        handlerCalled.Should().BeFalse("open circuit must fail-fast without calling the dependency");
    }

    [Fact]
    public async Task CircuitBreaker_DoesNotOpenBelowMinimumThroughput()
    {
        // Arrange
        var pipeline = CircuitBreakerPipeline();

        // Act — only 2 failures, below MinimumThroughput of 3 → circuit stays Closed
        for (var i = 0; i < 2; i++)
        {
            await pipeline.ExecuteAsync(async _ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }

        // Assert — 3rd call goes through normally
        var response = await pipeline.ExecuteAsync(async _ =>
            new HttpResponseMessage(HttpStatusCode.OK));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Timeout ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Timeout_CancelsCallExceedingBudget()
    {
        // Arrange: 50 ms budget, handler sleeps 300 ms
        var pipeline = TimeoutPipeline(ms: 50);

        // Act & Assert
        var act = async () => await pipeline.ExecuteAsync(async ct =>
        {
            await Task.Delay(300, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await act.Should().ThrowAsync<TimeoutRejectedException>();
    }

    [Fact]
    public async Task Timeout_DoesNotCancelCallWithinBudget()
    {
        // Arrange: generous 500 ms budget, handler returns immediately
        var pipeline = TimeoutPipeline(ms: 500);

        // Act
        var response = await pipeline.ExecuteAsync(async _ =>
            new HttpResponseMessage(HttpStatusCode.OK));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
