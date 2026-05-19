using OrderApi.Dtos;
using OrderApi.Models;
using System.Threading;
using System.Threading.Tasks;

namespace OrderApi.Services
{
    /// <summary>
    /// Interface for order-related business logic
    /// </summary>
    public interface IOrderService
    {
        /// <summary>
        /// Creates a new order with full validation, calculations, and persistence
        /// </summary>
        Task<Order> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken);
    }
}
