using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Data;

public class RefreshTokenRepository(AppDbContext db, IClock clock) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default)
        => db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeTokenAsync(RefreshToken token, string? replacedByHash, CancellationToken ct = default)
    {
        token.RevokedAt = clock.UtcNow;
        token.ReplacedByToken = replacedByHash;
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeFamilyAsync(string familyId, CancellationToken ct = default)
    {
        var tokens = await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var t in tokens)
            t.RevokedAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}
