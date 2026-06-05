using System.ComponentModel.DataAnnotations;

namespace QuotesApi.Models;

public class Quote
{
    public const int AuthorMaxLength = 100;
    public const int TextMaxLength = 1000;

    public int Id { get; set; }

    [Required]
    [MaxLength(AuthorMaxLength)]
    public string Author { get; set; } = string.Empty;

    [Required]
    [MaxLength(TextMaxLength)]
    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    // Null for quotes created before ownership tracking was introduced.
    public int? OwnerId { get; set; }

    public static (Quote? Quote, Dictionary<string, string[]>? Errors) Create(
        string author, string text, int? ownerId, DateTimeOffset createdAt)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(author))
            errors["author"] = ["Author is required"];
        else if (author.Length > AuthorMaxLength)
            errors["author"] = [$"Author must be {AuthorMaxLength} characters or fewer"];

        if (string.IsNullOrWhiteSpace(text))
            errors["text"] = ["Text is required"];
        else if (text.Length > TextMaxLength)
            errors["text"] = [$"Text must be {TextMaxLength} characters or fewer"];

        if (errors.Count > 0)
            return (null, errors);

        return (new Quote { Author = author, Text = text, OwnerId = ownerId, CreatedAt = createdAt }, null);
    }
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
