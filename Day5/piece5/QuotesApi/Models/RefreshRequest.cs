using System.Text.Json.Serialization;

namespace QuotesApi.Models;

public record RefreshRequest(
    [property: JsonPropertyName("refresh_token")] string RefreshToken);
