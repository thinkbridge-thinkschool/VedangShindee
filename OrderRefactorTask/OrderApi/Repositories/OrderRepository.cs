using Microsoft.EntityFrameworkCore;
using OrderApi.Data;
using OrderApi.Interfaces;
using OrderApi.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OrderApi.Repositories;

/// <summary>
/// Repository for Order data access using EF Core
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Adds a new order to the database
    /// </summary>
    public async Task<Order> AddOrderAsync(Order order, CancellationToken cancellationToken)
    {
        if (order == null)
        {
            throw new ArgumentNullException(nameof(order));
        }

        await _context.Orders.AddAsync(order, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return order;
    }

    /// <summary>
    /// Retrieves an order by its ID
    /// </summary>
    public async Task<Order?> GetOrderByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    /// <summary>
    /// Gets the most recent order by customer email within a specified time window
    /// </summary>
    public async Task<Order?> GetRecentOrderByEmailAsync(string email, int daysBack, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be empty", nameof(email));
        }

        var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);

        return await _context.Orders
            .Where(o => o.CustomerEmail == email && o.OrderDate > cutoffDate)
            .OrderByDescending(o => o.OrderDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets total inventory usage (sum of quantities from all items in all orders)
    /// </summary>
    public async Task<int> GetTotalInventoryUsageAsync(CancellationToken cancellationToken)
    {
        return await _context.Orders
            .SelectMany(o => o.Items)
            .SumAsync(i => i.Quantity, cancellationToken);
    }
}
