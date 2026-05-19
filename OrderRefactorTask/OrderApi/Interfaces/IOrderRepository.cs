using OrderApi.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OrderApi.Interfaces;

/// <summary>
/// Repository interface for Order data access
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Adds a new order to the repository
    /// </summary>
    Task<Order> AddOrderAsync(Order order, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves an order by ID
    /// </summary>
    Task<Order?> GetOrderByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the most recent order by customer email within the specified days window
    /// </summary>
    Task<Order?> GetRecentOrderByEmailAsync(string email, int daysBack, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the total inventory usage from all orders (sum of all quantities)
    /// </summary>
    Task<int> GetTotalInventoryUsageAsync(CancellationToken cancellationToken);
}