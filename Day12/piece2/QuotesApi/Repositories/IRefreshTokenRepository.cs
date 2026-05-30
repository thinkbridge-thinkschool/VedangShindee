using QuotesApi.Models;

namespace QuotesApi.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeTokenAsync(RefreshToken token, string? replacedByHash, CancellationToken ct = default);

    // Revokes every token in the family — called on reuse detection.
    Task RevokeFamilyAsync(string familyId, CancellationToken ct = default);
}
