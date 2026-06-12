// QuotesApi HybridCache Load Test
// Usage: dotnet run [base-url]   (default: http://localhost:5214)
//
// What it measures
// ────────────────
// Phase 1  Cold-cache STAMPEDE   — 50 concurrent requests, cache empty.
//          Only ONE factory reaches the DB; 49 callers await the in-flight task.
//
// Phase 2  Warm-cache concurrent — 50 concurrent requests, cache warm.
//          DB counter must be 0.  Shows zero DB load under high concurrency.
//
// Phase 3  No-cache baseline     — 20 SEQUENTIAL requests, cache evicted each time.
//          Each request hits the DB.  Sets the latency baseline.
//
// Phase 4  Paged-list stampede   — 30 concurrent requests for GET /api/quotes?page=1&size=10.
//
// Phase 5  Warm-cache sequential — 20 SEQUENTIAL requests, cache warm (NO eviction).
//          Fair apples-to-apples comparison with Phase 3.  DB count must be 0.
//          p99 here vs Phase 3 p99 = the true latency speedup.
//
// Requires: /diag/* endpoints running (mapped via MapDiagnosticsEndpoints in Program.cs).

using System.Diagnostics;
using System.Text.Json;

var baseUrl = args.Length > 0 ? args[0].TrimEnd('/') : "http://localhost:5214";
var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║     QuotesApi  ·  HybridCache + Stampede-protection      ║");
Console.WriteLine($"║     Target: {baseUrl,-44}║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ── helpers ──────────────────────────────────────────────────────────────────

async Task ResetCounter() =>
    await http.PostAsync("/diag/db-queries/reset", null);

async Task<long> ReadCounter()
{
    var json = await http.GetStringAsync("/diag/db-queries");
    var doc = JsonDocument.Parse(json);
    return doc.RootElement.GetProperty("count").GetInt64();
}

async Task EvictQuote(int id) =>
    await http.PostAsync($"/diag/cache/evict/{id}", null);

async Task EvictLists() =>
    await http.PostAsync("/diag/cache/evict-lists", null);

// Fire N concurrent GETs; returns sorted elapsed-ms array.
async Task<long[]> Blast(string url, int n)
{
    var tasks = Enumerable.Range(0, n).Select(_ => Task.Run(async () =>
    {
        var sw = Stopwatch.StartNew();
        var resp = await http.GetAsync(url);
        sw.Stop();
        resp.EnsureSuccessStatusCode();
        return sw.ElapsedMilliseconds;
    }));
    var results = await Task.WhenAll(tasks);
    Array.Sort(results);
    return results;
}

static string Stats(long[] ms)
{
    if (ms.Length == 0) return "n/a";
    long p50 = ms[(int)(ms.Length * 0.50)];
    long p95 = ms[(int)(ms.Length * 0.95)];
    long p99 = ms[Math.Min(ms.Length - 1, (int)(ms.Length * 0.99))];
    return $"p50={p50}ms  p95={p95}ms  p99={p99}ms";
}

static long P99(long[] ms) =>
    ms.Length == 0 ? 0 : ms[Math.Min(ms.Length - 1, (int)(ms.Length * 0.99))];

// ── verify API is reachable ───────────────────────────────────────────────────

Console.Write("Checking API … ");
int quoteId;
try
{
    var r = await http.GetAsync("/api/quotes?page=1&size=1");
    if (!r.IsSuccessStatusCode)
    {
        Console.WriteLine($"HTTP {(int)r.StatusCode}");
        return;
    }
    // Discover a real quote ID from the first page so we never hit a 404.
    var body = await r.Content.ReadAsStringAsync();
    var doc  = JsonDocument.Parse(body);
    var arr  = doc.RootElement.EnumerateArray().ToList();
    if (arr.Count == 0)
    {
        Console.WriteLine("FAILED — no quotes in DB. Start the API to trigger seeding.");
        return;
    }
    quoteId = arr[0].GetProperty("id").GetInt32();
    Console.WriteLine($"OK  (using quote id={quoteId})");
}
catch (Exception ex)
{
    Console.WriteLine($"FAILED — {ex.Message}");
    Console.WriteLine("Start the API first:  dotnet run --project QuotesApi");
    return;
}

// ── show cache backend ────────────────────────────────────────────────────────
try
{
    var infoJson = await http.GetStringAsync("/diag/cache-info");
    var info     = JsonDocument.Parse(infoJson).RootElement;
    var isRedis  = info.GetProperty("redisActive").GetBoolean();
    var l2       = info.GetProperty("l2").GetString();

    Console.WriteLine();
    Console.WriteLine("  Cache layers");
    Console.WriteLine($"  ├─ L1 : {info.GetProperty("l1").GetString()}");
    Console.WriteLine($"  ├─ L2 : {l2}");
    Console.WriteLine($"  ├─ Redis active : {(isRedis ? "YES ✓" : "NO  (using in-memory fallback)")}");
    Console.WriteLine($"  └─ Stampede protection : {info.GetProperty("stampede").GetString()}");
}
catch
{
    Console.WriteLine("  (cache-info endpoint unavailable)");
}
Console.WriteLine();

int QuoteId          = quoteId;
const int Concurrency   = 50;
const int BaselineN     = 20;
const int ListConc      = 30;

// ════════════════════════════════════════════════════════════════════════════
// Phase 1 — Cold-cache stampede
// ════════════════════════════════════════════════════════════════════════════

Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
Console.WriteLine($"│  Phase 1  Cold-cache STAMPEDE  (50 concurrent, quote #{QuoteId}) │");
Console.WriteLine("└─────────────────────────────────────────────────────────┘");
Console.WriteLine($"  Evicting quote #{QuoteId} from cache …");
await EvictQuote(QuoteId);
await ResetCounter();

Console.WriteLine($"  Firing {Concurrency} concurrent GET /api/quotes/{QuoteId} simultaneously …");
var p1Ms = await Blast($"/api/quotes/{QuoteId}", Concurrency);
var p1Db = await ReadCounter();

Console.WriteLine($"  DB queries : {p1Db,4}  (expected ≤ 1)");
Console.WriteLine($"  Latencies  : {Stats(p1Ms)}");

if (p1Db == 1)
{
    Console.WriteLine();
    Console.WriteLine("  ✓ STAMPEDE PROTECTION CONFIRMED");
    Console.WriteLine("    50 concurrent cold-cache misses → exactly 1 DB query.");
    Console.WriteLine("    49 callers awaited the in-flight factory task.");
}
else
{
    Console.WriteLine();
    Console.WriteLine($"  ✗ Expected 1 DB hit, got {p1Db}.");
}
Console.WriteLine();

// ════════════════════════════════════════════════════════════════════════════
// Phase 2 — Warm-cache (cache populated from Phase 1)
// ════════════════════════════════════════════════════════════════════════════

Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
Console.WriteLine("│  Phase 2  Warm-cache benchmark  (50 concurrent, hit)     │");
Console.WriteLine("└─────────────────────────────────────────────────────────┘");
await ResetCounter();

Console.WriteLine($"  Cache warm from Phase 1.  Firing {Concurrency} concurrent requests …");
var p2Ms = await Blast($"/api/quotes/{QuoteId}", Concurrency);
var p2Db = await ReadCounter();

Console.WriteLine($"  DB queries : {p2Db,4}  (expected 0)");
Console.WriteLine($"  Latencies  : {Stats(p2Ms)}");
Console.WriteLine();
if (p2Db == 0)
    Console.WriteLine("  ✓ ALL 50 REQUESTS SERVED FROM L1 MEMORY — zero DB queries.");
Console.WriteLine();

// ════════════════════════════════════════════════════════════════════════════
// Phase 3 — No-cache baseline (evict before every request, sequential)
// ════════════════════════════════════════════════════════════════════════════

Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
Console.WriteLine("│  Phase 3  No-cache baseline  (sequential, evict each)    │");
Console.WriteLine("└─────────────────────────────────────────────────────────┘");
await ResetCounter();
var p3List = new List<long>(BaselineN);

Console.WriteLine($"  Firing {BaselineN} sequential requests, evicting cache before each …");
for (int i = 0; i < BaselineN; i++)
{
    await EvictQuote(QuoteId);
    var sw = Stopwatch.StartNew();
    (await http.GetAsync($"/api/quotes/{QuoteId}")).EnsureSuccessStatusCode();
    sw.Stop();
    p3List.Add(sw.ElapsedMilliseconds);
}

var p3Ms = p3List.ToArray();
Array.Sort(p3Ms);
var p3Db = await ReadCounter();

Console.WriteLine($"  DB queries : {p3Db,4}  (expected {BaselineN})");
Console.WriteLine($"  Latencies  : {Stats(p3Ms)}");
Console.WriteLine();

// ════════════════════════════════════════════════════════════════════════════
// Phase 4 — Paged-list stampede
// ════════════════════════════════════════════════════════════════════════════

Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
Console.WriteLine("│  Phase 4  Paged-list STAMPEDE  (30 concurrent)           │");
Console.WriteLine("└─────────────────────────────────────────────────────────┘");
await EvictLists();
await ResetCounter();

Console.WriteLine($"  Evicted paged-list cache.  Firing {ListConc} concurrent requests …");
var p4Ms = await Blast("/api/quotes?page=1&size=10", ListConc);
var p4Db = await ReadCounter();

Console.WriteLine($"  DB queries : {p4Db,4}  (expected ≤ 1)");
Console.WriteLine($"  Latencies  : {Stats(p4Ms)}");
Console.WriteLine();
if (p4Db == 1)
    Console.WriteLine("  ✓ PAGED LIST STAMPEDE PROTECTED — 30 concurrent cold misses → 1 DB query.");
Console.WriteLine();

// ════════════════════════════════════════════════════════════════════════════
// Phase 5 — Sequential warm-cache (apples-to-apples with Phase 3)
// ════════════════════════════════════════════════════════════════════════════

Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
Console.WriteLine("│  Phase 5  Warm-cache sequential  (same N as Phase 3)     │");
Console.WriteLine("└─────────────────────────────────────────────────────────┘");

// Warm the cache with one request first, then measure sequential hits.
Console.WriteLine($"  Warming cache for quote #{QuoteId} …");
await EvictQuote(QuoteId);
(await http.GetAsync($"/api/quotes/{QuoteId}")).EnsureSuccessStatusCode();

await ResetCounter();
var p5List = new List<long>(BaselineN);

Console.WriteLine($"  Firing {BaselineN} sequential requests, cache warm, NO eviction …");
for (int i = 0; i < BaselineN; i++)
{
    var sw = Stopwatch.StartNew();
    (await http.GetAsync($"/api/quotes/{QuoteId}")).EnsureSuccessStatusCode();
    sw.Stop();
    p5List.Add(sw.ElapsedMilliseconds);
}

var p5Ms = p5List.ToArray();
Array.Sort(p5Ms);
var p5Db = await ReadCounter();

Console.WriteLine($"  DB queries : {p5Db,4}  (expected 0 — all from L1 memory)");
Console.WriteLine($"  Latencies  : {Stats(p5Ms)}");
Console.WriteLine();
if (p5Db == 0)
    Console.WriteLine("  ✓ ZERO DB QUERIES — all 20 requests served from L1 in-process cache.");
Console.WriteLine();

// ════════════════════════════════════════════════════════════════════════════
// Summary table
// ════════════════════════════════════════════════════════════════════════════

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  RESULTS SUMMARY                                         ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine($"  {"Scenario",-38}  {"DB queries",10}  {"p50",6}  {"p99",6}");
Console.WriteLine($"  {"───────────────────────────────────────────────────────────────"}");

static long P50(long[] ms) => ms.Length == 0 ? 0 : ms[(int)(ms.Length * 0.50)];

Console.WriteLine($"  {"Phase 3  No-cache baseline     (sequential N=" + BaselineN  + ")",-38}  {p3Db,10}  {P50(p3Ms),5}ms  {P99(p3Ms),5}ms");
Console.WriteLine($"  {"Phase 5  Warm-cache sequential (sequential N=" + BaselineN  + ")",-38}  {p5Db,10}  {P50(p5Ms),5}ms  {P99(p5Ms),5}ms  ← fair compare");
Console.WriteLine($"  {"Phase 1  Stampede cold miss    (concurrent N=" + Concurrency + ")",-38}  {p1Db,10}  {P50(p1Ms),5}ms  {P99(p1Ms),5}ms");
Console.WriteLine($"  {"Phase 2  Warm-cache concurrent (concurrent N=" + Concurrency + ")",-38}  {p2Db,10}  {P50(p2Ms),5}ms  {P99(p2Ms),5}ms");
Console.WriteLine($"  {"Phase 4  Paged-list stampede   (concurrent N=" + ListConc    + ")",-38}  {p4Db,10}  {P50(p4Ms),5}ms  {P99(p4Ms),5}ms");
Console.WriteLine();

// DB load reduction: stampede vs no-cache baseline (both at their respective concurrency)
if (p3Db > 0)
{
    var pct = (double)(p3Db - p1Db) / p3Db * 100;
    Console.WriteLine($"  DB load reduction under stampede : {pct:F0}%  ({p3Db} sequential → {p1Db} concurrent)");
}

// ── True latency speedup: Phase 3 vs Phase 5 — both sequential, same N ──────
long p99Baseline = P99(p3Ms);
long p99Warm     = P99(p5Ms);
long p50Baseline = P50(p3Ms);
long p50Warm     = P50(p5Ms);

Console.WriteLine();
Console.WriteLine("  ── Apples-to-apples latency comparison (both sequential, N=20) ──");
Console.WriteLine($"  Phase 3  No-cache   p50={p50Baseline}ms  p99={p99Baseline}ms  (every request hits DB)");
Console.WriteLine($"  Phase 5  Warm-cache p50={p50Warm}ms  p99={p99Warm}ms  (every request from L1 memory)");
Console.WriteLine();

if (p99Baseline > 0 && p99Warm == 0)
    Console.WriteLine($"  p99 speedup : >{p99Baseline}x  ({p99Baseline}ms → <1ms)");
else if (p99Baseline > 0 && p99Warm > 0)
    Console.WriteLine($"  p99 speedup : {(double)p99Baseline / p99Warm:F1}x  ({p99Baseline}ms → {p99Warm}ms)");

if (p50Baseline > 0 && p50Warm == 0)
    Console.WriteLine($"  p50 speedup : >{p50Baseline}x  ({p50Baseline}ms → <1ms)");
else if (p50Baseline > 0 && p50Warm > 0)
    Console.WriteLine($"  p50 speedup : {(double)p50Baseline / p50Warm:F1}x  ({p50Baseline}ms → {p50Warm}ms)");

Console.WriteLine();

// Re-read cache info for the footer
try
{
    var infoJson = await http.GetStringAsync("/diag/cache-info");
    var info     = JsonDocument.Parse(infoJson).RootElement;
    var isRedis  = info.GetProperty("redisActive").GetBoolean();
    var l2       = info.GetProperty("l2").GetString();

    if (isRedis)
    {
        Console.WriteLine($"  Cache backend  : {l2}  ✓ Redis active");
        Console.WriteLine("  L2 entries survive API restarts and are shared across pods.");
    }
    else
    {
        Console.WriteLine($"  Cache backend  : {l2}  (in-memory fallback)");
        Console.WriteLine("  To switch to Redis:");
        Console.WriteLine("    docker run -d -p 6379:6379 redis:alpine");
        Console.WriteLine("    Set ConnectionStrings:Redis=localhost:6379 in appsettings.json");
    }
}
catch { }
