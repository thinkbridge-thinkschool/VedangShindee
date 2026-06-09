using System.Threading.Channels;

namespace QuotesApi.Services;

// Singleton: one channel for the process lifetime, many writers, one reader (the worker).
public sealed class QuoteAuditQueue
{
    private readonly Channel<QuoteAuditEvent> _channel =
        Channel.CreateUnbounded<QuoteAuditEvent>(
            new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<QuoteAuditEvent> Reader => _channel.Reader;

    // Fire-and-forget safe: TryWrite on an unbounded channel never blocks or drops.
    public void Enqueue(QuoteAuditEvent evt) => _channel.Writer.TryWrite(evt);
}
