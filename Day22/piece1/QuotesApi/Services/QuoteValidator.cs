using QuotesApi.Models;

namespace QuotesApi.Services;

public class QuoteValidator : IQuoteValidator
{
    public Dictionary<string, string[]> Validate(CreateQuoteRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Author))
            errors["author"] = ["Author is required"];
        else if (request.Author.Length > 200)
            errors["author"] = ["Author must be 200 characters or less"];

        if (string.IsNullOrWhiteSpace(request.Text))
            errors["text"] = ["Text is required"];
        else if (request.Text.Length > 1000)
            errors["text"] = ["Quote text must be 1000 characters or less"];

        return errors;
    }
}
