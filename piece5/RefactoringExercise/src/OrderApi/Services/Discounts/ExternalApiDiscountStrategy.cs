using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OrderApi.Services.Discounts
{
    public class ExternalApiDiscountStrategy : IDiscountStrategy
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ExternalApiDiscountStrategy> _logger;

        // Settable so OrderService can supply the runtime code before calling Apply.
        // Safe because this class is registered as Scoped (one instance per request).
        public string? Code { get; set; }

        public ExternalApiDiscountStrategy(
            IHttpClientFactory httpClientFactory,
            ILogger<ExternalApiDiscountStrategy> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<decimal> Apply(decimal subtotal, CancellationToken cancellationToken = default)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync(
                    $"https://api.discount-checker.com/validate?code={Code}", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (content.Contains("valid"))
                        return subtotal - 5.0m;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Failed to validate discount code {Code}. Ignoring discount.", Code);
            }

            return subtotal;
        }
    }
}
