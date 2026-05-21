using QuotesApi.Models;

namespace QuotesApi.Services;

public class QuoteValidator : IQuoteValidator
{
    public Dictionary<string, string[]> Validate(CreateQuoteRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Author))
            errors["author"] = ["Author is required"];

        if (string.IsNullOrWhiteSpace(request.Text))
            errors["text"] = ["Text is required"];

        return errors;
    }
}
