using System.Threading;
using System.Threading.Tasks;

namespace OrderApi.Services.Discounts
{
    public class Save10Strategy : IDiscountStrategy
    {
        public string Code => "SAVE10";

        public Task<decimal> Apply(decimal subtotal, CancellationToken cancellationToken = default)
            => Task.FromResult(subtotal * 0.9m);
    }
}
