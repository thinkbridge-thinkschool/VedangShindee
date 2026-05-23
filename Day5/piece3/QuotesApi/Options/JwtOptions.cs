namespace QuotesApi.Options;

public record JwtOptions
{
    // Secret: never commit a real value here. Local dev: dotnet user-secrets set "Jwt:Key" "..."
    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(7);
}
