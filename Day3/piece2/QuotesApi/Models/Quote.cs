using System.ComponentModel.DataAnnotations;

namespace QuotesApi.Models;

public class Quote
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Author { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    // Null for quotes created before ownership tracking was introduced.
    public int? OwnerId { get; set; }
}

public class CreateQuoteDto
{
    [Required]
    [MaxLength(100)]
    public string Author { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Text { get; set; } = string.Empty;
}
