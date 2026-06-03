using QuotesApi.Models;

namespace QuotesApi.Services;

public interface IQuoteValidator
{
    Dictionary<string, string[]> Validate(CreateQuoteRequest request);
}
