namespace QuotesApi.Models;

public class Quote
{
    public int Id { get; private set; }
    public string Author { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }

    private Quote() { }

    public static (Quote? Quote, DomainError? Error) Create(string author, string text)
    {
        var trimmedAuthor = author?.Trim() ?? string.Empty;
        var trimmedText = text?.Trim() ?? string.Empty;

        if (trimmedAuthor.Length is < 1 or > 200)
            return (null, new DomainError("Author must be between 1 and 200 characters."));

        if (trimmedText.Length is < 1 or > 1000)
            return (null, new DomainError("Text must be between 1 and 1000 characters."));

        return (new Quote { Author = trimmedAuthor, Text = trimmedText }, null);
    }

    public void SoftDelete() => IsDeleted = true;
}

public record DomainError(string Message);
