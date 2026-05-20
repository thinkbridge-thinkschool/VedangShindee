using System.Threading;
using System.Threading.Tasks;

namespace OrderApi.Services.Discounts
{
    public class Save20Strategy : IDiscountStrategy
    {
        public string Code => "SAVE20";

        public Task<decimal> Apply(decimal subtotal, CancellationToken cancellationToken = default)
            => Task.FromResult(subtotal * 0.8m);
    }
}
