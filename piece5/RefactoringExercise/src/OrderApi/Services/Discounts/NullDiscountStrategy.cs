using System.Threading;
using System.Threading.Tasks;

namespace OrderApi.Services.Discounts
{
    public class NullDiscountStrategy : IDiscountStrategy
    {
        public string Code => string.Empty;

        public Task<decimal> Apply(decimal subtotal, CancellationToken cancellationToken = default)
            => Task.FromResult(subtotal);
    }
}
