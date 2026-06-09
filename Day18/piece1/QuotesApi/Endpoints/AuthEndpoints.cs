using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Options;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            AppDbContext db,
            IRefreshTokenRepository tokenRepo,
            IClock clock,
            IOptions<JwtOptions> jwtOptions,
            ILogger<Program> logger) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                logger.LogWarning("Failed login attempt for email {Email}", request.Email);
                return Results.Unauthorized();
            }

            var jwt = jwtOptions.Value;
            var accessToken = MintAccessToken(user, jwt);
            var (rawRefresh, refreshEntity) = MintRefreshToken(user.Id, Guid.NewGuid().ToString(), clock, jwt);

            await tokenRepo.AddAsync(refreshEntity);

            logger.LogInformation("User {UserId} logged in successfully", user.Id);

            return Results.Ok(new LoginResponse(accessToken, rawRefresh, (int)jwt.AccessTokenLifetime.TotalSeconds));
        });

        app.MapPost("/api/auth/refresh", async (
            RefreshRequest request,
            IRefreshTokenRepository tokenRepo,
            IClock clock,
            IOptions<JwtOptions> jwtOptions,
            ILogger<Program> logger,
            AppDbContext db) =>
        {
            var incomingHash = TokenHasher.Hash(request.RefreshToken);
            var stored = await tokenRepo.FindByHashAsync(incomingHash);

            if (stored is null)
                return Results.Unauthorized();

            // Reuse detected: token was already rotated away — revoke entire family.
            if (stored.RevokedAt is not null)
            {
                logger.LogWarning(
                    "Refresh token reuse detected for family {FamilyId}, UserId {UserId}. Revoking entire chain.",
                    stored.FamilyId, stored.UserId);

                await tokenRepo.RevokeFamilyAsync(stored.FamilyId);
                return Results.Unauthorized();
            }

            if (stored.ExpiresAt < clock.UtcNow)
                return Results.Unauthorized();

            var user = await db.Users.FindAsync(stored.UserId);
            if (user is null)
                return Results.Unauthorized();

            var jwt = jwtOptions.Value;
            var newAccessToken = MintAccessToken(user, jwt);
            var (newRawRefresh, newRefreshEntity) = MintRefreshToken(user.Id, stored.FamilyId, clock, jwt);

            // Atomic: revoke old token (pointing at its replacement) and persist the new one.
            await tokenRepo.RevokeTokenAsync(stored, newRefreshEntity.TokenHash);
            await tokenRepo.AddAsync(newRefreshEntity);

            logger.LogInformation("Refresh token rotated for user {UserId} in family {FamilyId}", user.Id, stored.FamilyId);

            return Results.Ok(new LoginResponse(newAccessToken, newRawRefresh, (int)jwt.AccessTokenLifetime.TotalSeconds));
        });

        app.MapPost("/api/auth/logout", async (
            RefreshRequest request,
            IRefreshTokenRepository tokenRepo,
            ILogger<Program> logger) =>
        {
            var hash = TokenHasher.Hash(request.RefreshToken);
            var stored = await tokenRepo.FindByHashAsync(hash);

            if (stored is not null && stored.RevokedAt is null)
            {
                await tokenRepo.RevokeTokenAsync(stored, replacedByHash: null);
                logger.LogInformation("User {UserId} logged out, token family {FamilyId} revoked", stored.UserId, stored.FamilyId);
            }

            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }

    private static string MintAccessToken(User user, JwtOptions jwt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("scope", "quotes.write")
            ],
            expires: DateTime.UtcNow.Add(jwt.AccessTokenLifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static (string rawToken, RefreshToken entity) MintRefreshToken(
        int userId, string familyId, IClock clock, JwtOptions jwt)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var entity = new RefreshToken
        {
            TokenHash = TokenHasher.Hash(raw),
            UserId = userId,
            FamilyId = familyId,
            ExpiresAt = clock.UtcNow.Add(jwt.RefreshTokenLifetime)
        };
        return (raw, entity);
    }
}
