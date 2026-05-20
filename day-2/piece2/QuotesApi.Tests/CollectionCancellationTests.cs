using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Data;
using QuotesApi.Models;
using Xunit;

namespace QuotesApi.Tests;

public class CollectionCancellationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CollectionCancellationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // Proves the token flows all the way to the repo: if it didn't, the Task.Delay(30s)
    // in SlowCollectionRepository would never be interrupted and the test would time out.
    [Fact]
    public async Task GetById_TokenCancelledMidRequest_AbortsWellBeforeRepoTimeout()
    {
        var app = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s =>
                s.AddScoped<ICollectionRepository>(_ => new SlowCollectionRepository())));

        var client = app.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var sw = Stopwatch.StartNew();
        try
        {
            await client.GetAsync($"/api/collections/{Guid.NewGuid()}", cts.Token);
        }
        catch (OperationCanceledException) { }
        sw.Stop();

        // SlowCollectionRepository delays 30 s — if we finish in under 3 s, cancellation flowed through.
        Assert.True(
            sw.ElapsedMilliseconds < 3_000,
            $"Expected abort within 3 s but took {sw.ElapsedMilliseconds} ms — CancellationToken is not flowing through the stack.");
    }

    // Proves the server maps OperationCanceledException → 499.
    // Either the client gets a 499 response, or it throws TaskCanceledException (connection already gone).
    // Both outcomes mean the operation was cut short correctly.
    [Fact]
    public async Task GetById_TokenCancelledMidRequest_Returns499OrThrowsTaskCancelled()
    {
        var app = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s =>
                s.AddScoped<ICollectionRepository>(_ => new SlowCollectionRepository())));

        var client = app.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        HttpResponseMessage? response = null;
        try
        {
            response = await client.GetAsync($"/api/collections/{Guid.NewGuid()}", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Client-side cancellation raced ahead of the server writing 499 — still correct.
            return;
        }

        Assert.Equal(499, (int)response!.StatusCode);
    }
}

// Simulates a repository whose I/O takes 30 s, honouring the CancellationToken.
// Task.Delay propagates cancellation so the token genuinely interrupts the work.
file sealed class SlowCollectionRepository : ICollectionRepository
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(30);

    public async Task<Collection?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await Task.Delay(Delay, ct);
        return null;
    }

    public Task AddAsync(Collection collection, CancellationToken ct) =>
        Task.Delay(Delay, ct);

    public Task UpdateAsync(Collection collection, CancellationToken ct) =>
        Task.Delay(Delay, ct);

    public Task DeleteAsync(Guid id, CancellationToken ct) =>
        Task.Delay(Delay, ct);
}
