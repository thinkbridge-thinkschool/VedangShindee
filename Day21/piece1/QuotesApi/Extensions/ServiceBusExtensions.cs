using Azure.Messaging.ServiceBus;
using QuotesApi.Messaging;
using QuotesApi.Messaging.Workers;
using QuotesApi.Options;
using QuotesApi.Outbox;

namespace QuotesApi.Extensions;

public static class ServiceBusExtensions
{
    public static void AddServiceBus(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServiceBusOptions>(configuration.GetSection("ServiceBus"));

        var opts = configuration.GetSection("ServiceBus").Get<ServiceBusOptions>()
                   ?? new ServiceBusOptions();

        // ── Idempotency store (singleton) ─────────────────────────────────────────
        // Always registered — the /sb/idempotency endpoint reads it even when workers are off.
        services.AddSingleton<IdempotencyStore>();

        // ── Outbox relay — always registered ─────────────────────────────────────
        // The relay must run regardless of whether Service Bus is configured so that
        // outbox rows never accumulate silently.  Without a connection string the
        // NullQuoteEventPublisher logs a warning per pending row; with one it delivers.
        services.AddHostedService<OutboxRelayWorker>();

        if (string.IsNullOrWhiteSpace(opts.ConnectionString))
        {
            // No broker — log pending rows as warnings so the gap is visible.
            services.AddSingleton<IQuoteEventPublisher, NullQuoteEventPublisher>();
            return;
        }

        // ── ServiceBusClient (singleton) ─────────────────────────────────────────
        // One client per process: manages the underlying AMQP connection pool.
        services.AddSingleton(_ => new ServiceBusClient(opts.ConnectionString));

        // ── Publisher (registered against the interface for testability) ─────────
        services.AddSingleton<IQuoteEventPublisher, QuoteEventPublisher>();

        // ── Subscription workers ──────────────────────────────────────────────────
        services.AddHostedService<EmailNotificationWorker>();
        services.AddHostedService<AuditLogWorker>();

        // ── DLQ monitor ───────────────────────────────────────────────────────────
        // Singleton so ServiceBusDemoEndpoints can inject it for the /sb/dlq snapshot.
        services.AddSingleton<DlqMonitorWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<DlqMonitorWorker>());
    }
}
