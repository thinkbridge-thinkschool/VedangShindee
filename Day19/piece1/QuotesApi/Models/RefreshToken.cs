using System.ComponentModel.DataAnnotations;

namespace QuotesApi.Models;

public class RefreshToken
{
    public int Id { get; set; }

    [Required]
    public string TokenHash { get; set; } = string.Empty;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required]
    public string FamilyId { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
}
