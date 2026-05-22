using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Data;

public class QuoteRepository : IQuoteRepository
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public QuoteRepository(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<List<Quote>> GetPagedAsync(int page, int size, CancellationToken cancellationToken)
    {
        return await _db.Quotes
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
    }

    public async Task<Quote?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _db.Quotes.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    public async Task<Quote> CreateAsync(Quote quote, CancellationToken cancellationToken)
    {
        quote.CreatedAt = _clock.UtcNow;
        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync(cancellationToken);
        return quote;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var quote = await _db.Quotes.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (quote is null) return false;
        _db.Quotes.Remove(quote);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
