using System.Threading;
using System.Threading.Tasks;

namespace OrderApi.Services.Discounts
{
    public interface IDiscountStrategy
    {
        string? Code { get; }
        Task<decimal> Apply(decimal subtotal, CancellationToken cancellationToken = default);
    }
}
