using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using QuotesApi.Options;

namespace QuotesApi.Messaging.Workers;

// ── Background worker that peeks the DLQ every 30 seconds ──────────────────────
//
// The dead-letter sub-queue lives at:
//   {topic}/Subscriptions/{subscription}/$DeadLetterQueue
//
// We peek (non-destructive) rather than receive so the DLQ messages stay visible
// for human inspection / reprocessing tooling.  Peeking does NOT lock or settle.
//
// In production you would typically:
//   1. Alert on DLQ depth > 0 (e.g., via Azure Monitor metric alert)
//   2. Build a separate reprocessing job that Receives → fixes → re-publishes
public sealed class DlqMonitorWorker : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusOptions _opts;
    private readonly ILogger<DlqMonitorWorker> _logger;

    // Expose the most-recently-peeked DLQ messages so the /sb/dlq endpoint can return them.
    private readonly List<DlqMessageSummary> _emailDlq = [];
    private readonly List<DlqMessageSummary> _auditDlq = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public DlqMonitorWorker(ServiceBusClient client, IOptions<ServiceBusOptions> opts,
        ILogger<DlqMonitorWorker> logger)
    {
        _client = client;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DlqMessageSummary>> GetEmailDlqAsync()
    {
        await _lock.WaitAsync();
        try { return [.._emailDlq]; }
        finally { _lock.Release(); }
    }

    public async Task<IReadOnlyList<DlqMessageSummary>> GetAuditDlqAsync()
    {
        await _lock.WaitAsync();
        try { return [.._auditDlq]; }
        finally { _lock.Release(); }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[DlqMonitor] Started — polling DLQ every 30 s");

        while (!stoppingToken.IsCancellationRequested)
        {
            await PeekDlqAsync(stoppingToken);

            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("[DlqMonitor] Stopped");
    }

    private async Task PeekDlqAsync(CancellationToken ct)
    {
        // DLQ sub-queue path convention: {topic}/Subscriptions/{subscription}/$DeadLetterQueue
        var emailDlqPath =
            $"{_opts.TopicName}/Subscriptions/{_opts.EmailSubscriptionName}/$DeadLetterQueue";

        var auditDlqPath =
            $"{_opts.TopicName}/Subscriptions/{_opts.AuditSubscriptionName}/$DeadLetterQueue";

        var emailMessages = await PeekSubQueueAsync(emailDlqPath, ct);
        var auditMessages = await PeekSubQueueAsync(auditDlqPath, ct);

        await _lock.WaitAsync(ct);
        try
        {
            _emailDlq.Clear();
            _emailDlq.AddRange(emailMessages);
            _auditDlq.Clear();
            _auditDlq.AddRange(auditMessages);
        }
        finally { _lock.Release(); }

        if (emailMessages.Count > 0 || auditMessages.Count > 0)
        {
            _logger.LogWarning(
                "[DlqMonitor] DLQ snapshot — email-notifications: {EmailCount} msg(s), audit-log: {AuditCount} msg(s)",
                emailMessages.Count, auditMessages.Count);

            foreach (var m in emailMessages)
                _logger.LogWarning(
                    "[DlqMonitor][email-DLQ] MessageId={MessageId} Reason={Reason} Description={Desc}",
                    m.MessageId, m.DeadLetterReason, m.DeadLetterDescription);

            foreach (var m in auditMessages)
                _logger.LogWarning(
                    "[DlqMonitor][audit-DLQ] MessageId={MessageId} Reason={Reason} Description={Desc}",
                    m.MessageId, m.DeadLetterReason, m.DeadLetterDescription);
        }
        else
        {
            _logger.LogDebug("[DlqMonitor] DLQ is empty on both subscriptions");
        }
    }

    private async Task<List<DlqMessageSummary>> PeekSubQueueAsync(string subQueuePath, CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(subQueuePath,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });

        try
        {
            var peeked = await receiver.PeekMessagesAsync(maxMessages: 10, cancellationToken: ct);
            return peeked.Select(m => new DlqMessageSummary(
                m.MessageId,
                m.Subject,
                m.DeadLetterReason,
                m.DeadLetterErrorDescription,
                m.EnqueuedTime,
                m.Body.ToString())).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Swallow — namespace might be unreachable; log and return empty so the app stays up.
            _logger.LogWarning(ex, "[DlqMonitor] Could not peek {Path}", subQueuePath);
            return [];
        }
        finally
        {
            await receiver.DisposeAsync();
        }
    }
}

public sealed record DlqMessageSummary(
    string MessageId,
    string? Subject,
    string? DeadLetterReason,
    string? DeadLetterDescription,
    DateTimeOffset EnqueuedTime,
    string Body);
