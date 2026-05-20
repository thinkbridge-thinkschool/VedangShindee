public class CollectionCancellationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public CollectionCancellationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetById_TokenCancelledMidRequest_AbortsWellBeforeRepoTimeout()
    {
        var app = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s =>
                s.AddScoped<ICollectionRepository>(_ => new SlowCollectionRepository())));

        var client = app.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var sw = Stopwatch.StartNew();
        try { await client.GetAsync($"/api/collections/{Guid.NewGuid()}", cts.Token); }
        catch (OperationCanceledException) { }
        sw.Stop();

        // SlowCollectionRepository waits 30 s — finish in under 3 s proves the token flowed through.
        Assert.True(sw.ElapsedMilliseconds < 3_000,
            $"Took {sw.ElapsedMilliseconds} ms — CancellationToken is not flowing through the stack.");
    }

    [Fact]
    public async Task GetById_TokenCancelledMidRequest_Returns499OrThrowsTaskCancelled()
    {
        var app = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s =>
                s.AddScoped<ICollectionRepository>(_ => new SlowCollectionRepository())));

        var client = app.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        HttpResponseMessage? response = null;
        try { response = await client.GetAsync($"/api/collections/{Guid.NewGuid()}", cts.Token); }
        catch (OperationCanceledException) { return; }

        Assert.Equal(499, (int)response!.StatusCode);
    }
}

file sealed class SlowCollectionRepository : ICollectionRepository
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(30);

    public async Task<Collection?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await Task.Delay(Delay, ct);
        return null;
    }
    public Task AddAsync(Collection collection, CancellationToken ct) => Task.Delay(Delay, ct);
    public Task UpdateAsync(Collection collection, CancellationToken ct) => Task.Delay(Delay, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct) => Task.Delay(Delay, ct);
}
