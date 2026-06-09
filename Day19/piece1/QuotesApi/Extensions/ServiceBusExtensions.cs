using Azure.Messaging.ServiceBus;
using QuotesApi.Messaging;
using QuotesApi.Messaging.Workers;
using QuotesApi.Options;

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

        if (string.IsNullOrWhiteSpace(opts.ConnectionString))
        {
            // No connection string — skip the client, publisher, and workers entirely.
            // The app starts cleanly; /sb/* endpoints return a 503 or empty data.
            // Integration tests and local runs without a namespace hit this path.
            return;
        }

        // ── ServiceBusClient (singleton) ─────────────────────────────────────────
        // One client per process: manages the underlying AMQP connection pool.
        services.AddSingleton(_ => new ServiceBusClient(opts.ConnectionString));

        // ── Publisher ─────────────────────────────────────────────────────────────
        services.AddSingleton<QuoteEventPublisher>();

        // ── Subscription workers ──────────────────────────────────────────────────
        services.AddHostedService<EmailNotificationWorker>();
        services.AddHostedService<AuditLogWorker>();

        // ── DLQ monitor ───────────────────────────────────────────────────────────
        // Singleton so ServiceBusDemoEndpoints can inject it for the /sb/dlq snapshot.
        services.AddSingleton<DlqMonitorWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<DlqMonitorWorker>());
    }
}
