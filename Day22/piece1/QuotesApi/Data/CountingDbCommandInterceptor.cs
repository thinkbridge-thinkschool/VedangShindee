using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuotesApi.Services;

namespace QuotesApi.Data;

// Singleton interceptor — counts only SELECT queries that touch the Quotes table.
// Background workers (OutboxRelayWorker, etc.) query other tables and are excluded,
// so the /diag/db-queries counter reflects quote-endpoint DB load only.
public sealed class CountingDbCommandInterceptor : DbCommandInterceptor
{
    private readonly DbQueryCounter _counter;

    public CountingDbCommandInterceptor(DbQueryCounter counter) => _counter = counter;

    private static bool IsQuoteRead(DbCommand command) =>
        command.CommandText.Contains("Quotes", StringComparison.OrdinalIgnoreCase) &&
        command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (IsQuoteRead(command))
            _counter.Increment();
        return new ValueTask<DbDataReader>(result);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        if (IsQuoteRead(command))
            _counter.Increment();
        return result;
    }
}
