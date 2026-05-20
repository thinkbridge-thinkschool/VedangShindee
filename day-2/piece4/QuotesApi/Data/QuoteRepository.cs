using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;

namespace QuotesApi.Data;

public class QuoteRepository : IQuoteRepository
{
    private readonly QuoteDbContext _dbContext;
    private readonly ILogger<QuoteRepository> _logger;

    public QuoteRepository(QuoteDbContext dbContext, ILogger<QuoteRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<(IEnumerable<Quote> Quotes, int TotalCount)> GetPaginatedAsync(int page, int size, CancellationToken ct)
    {
        _logger.LogInformation("Fetching quotes page {Page} with size {Size}", page, size);
        var query = _dbContext.Quotes.AsNoTracking().Where(q => !q.IsDeleted);
        var total = await query.CountAsync(ct);
        var quotes = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return (quotes, total);
    }

    public async Task<Quote?> GetByIdAsync(int id, CancellationToken ct)
    {
        _logger.LogInformation("Fetching quote {Id}", id);
        var quote = await _dbContext.Quotes.FindAsync(new object[] { id }, ct);
        return quote is { IsDeleted: false } ? quote : null;
    }

    public async Task<Quote> AddAsync(Quote quote, CancellationToken ct)
    {
        _logger.LogInformation("Adding new quote by {Author}", quote.Author);
        _dbContext.Quotes.Add(quote);
        await _dbContext.SaveChangesAsync(ct);
        return quote;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        _logger.LogInformation("Soft-deleting quote {Id}", id);
        var quote = await _dbContext.Quotes.FindAsync(new object[] { id }, ct);
        if (quote is null || quote.IsDeleted) return false;

        quote.SoftDelete();
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }
}
