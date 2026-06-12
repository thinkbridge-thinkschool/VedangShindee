using System.Text.Json;
using QuotesApi.Data;
using QuotesApi.Messaging.Models;
using QuotesApi.Models;
using QuotesApi.Outbox;
using QuotesApi.Services;

namespace QuotesApi.Commands;

public class CreateQuoteHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IQuoteValidator _validator;
    private readonly QuoteAuditQueue _auditQueue;

    public CreateQuoteHandler(AppDbContext db, IClock clock, IQuoteValidator validator, QuoteAuditQueue auditQueue)
    {
        _db = db;
        _clock = clock;
        _validator = validator;
        _auditQueue = auditQueue;
    }

    public async Task<(CreateQuoteResult? Result, Dictionary<string, string[]>? Errors)> HandleAsync(
        CreateQuoteCommand command, CancellationToken ct)
    {
        var errors = _validator.Validate(new CreateQuoteRequest { Author = command.Author, Text = command.Text });
        if (errors.Count > 0)
            return (null, errors);

        var quote = new Quote
        {
            Author = command.Author,
            Text = command.Text,
            OwnerId = command.OwnerId,
            CreatedAt = _clock.UtcNow,
        };

        // ── Atomic write ─────────────────────────────────────────────────────────
        // Both rows land in a single DB transaction.  Either both commit or neither
        // does — there is no state where the Quote exists without a relay row.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Step 1: persist the Quote so EF populates quote.Id via IDENTITY.
        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync(ct);

        // Step 2: build the event now that we know the real QuoteId.
        // The EventId doubles as the outbox row's PK and the ServiceBus MessageId,
        // giving the idempotent consumer a stable dedup key across every retry.
        var eventId = Guid.NewGuid();
        var evt = new QuoteCreatedEvent
        {
            EventId    = eventId.ToString(),
            QuoteId    = quote.Id,
            Author     = quote.Author,
            Text       = quote.Text,
            PublishedBy = command.OwnerId?.ToString() ?? "",
            OccurredAt = quote.CreatedAt,
        };

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id        = eventId,           // same Guid → relay sets MessageId = eventId
            Topic     = "quote-created",
            Payload   = JsonSerializer.Serialize(evt),
            CreatedAt = quote.CreatedAt,
        });
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        // ── End atomic write ────────────────────────────────────────────────────

        _auditQueue.Enqueue(new QuoteAuditEvent("created", quote.Id, quote.Author, command.OwnerId, quote.CreatedAt));

        return (new CreateQuoteResult(quote.Id, quote.Author, quote.Text, quote.CreatedAt), null);
    }
}
