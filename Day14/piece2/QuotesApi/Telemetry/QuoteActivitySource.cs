using System.Diagnostics;

namespace QuotesApi.Telemetry;

public static class QuoteActivitySource
{
    public const string Name = "QuotesApi";
    public static readonly ActivitySource Instance = new(Name, "1.0.0");
}
