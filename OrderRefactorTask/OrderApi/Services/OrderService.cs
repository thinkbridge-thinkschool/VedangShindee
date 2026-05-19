using Microsoft.Extensions.Logging;
using OrderApi.Dtos;
using OrderApi.Interfaces;
using OrderApi.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OrderApi.Services
{
    /// <summary>
    /// Service for order business logic
    /// </summary>
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly ILogger<OrderService> _logger;

        // Business rule constants
        private const decimal TAX_RATE = 0.10m;
        private const int INVENTORY_LIMIT = 10000;
        private const int DUPLICATE_ORDER_WINDOW_DAYS = 7;
        private const int ESTIMATED_DELIVERY_DAYS = 5;

        public OrderService(IOrderRepository orderRepository, ILogger<OrderService> logger)
        {
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new order with validation, business logic, and persistence
        /// </summary>
        public async Task<Order> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
        {
            try
            {
                // Validate input
                ValidateRequest(request);

                // Check for duplicate orders
                await CheckForDuplicateOrderAsync(request.Email, cancellationToken);

                // Create order items from DTOs
                var orderItems = ConvertToOrderItems(request.Items);

                // Calculate totals
                var (subtotal, tax, discount) = CalculatePricing(orderItems, request.DiscountCode);
                var finalPrice = subtotal + tax - discount;

                // Validate inventory
                await ValidateInventoryAsync(orderItems, cancellationToken);

                // Create order entity
                var order = new Order
                {
                    CustomerName = request.Name,
                    CustomerEmail = request.Email,
                    ShippingAddress = request.Address,
                    TotalAmount = finalPrice,
                    OrderDate = DateTime.UtcNow,
                    Status = "Pending",
                    Items = orderItems
                };

                // Persist to repository
                var createdOrder = await _orderRepository.AddOrderAsync(order, cancellationToken);

                _logger.LogInformation("Order {OrderId} created successfully for customer {Email}", 
                    createdOrder.Id, request.Email);

                return createdOrder;
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "Validation failed for order creation from {Email}", request.Email);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating order for {Email}", request.Email);
                throw new InvalidOperationException("Failed to create order. Please try again.", ex);
            }
        }

        /// <summary>
        /// Validates the request has all required fields and valid ranges
        /// </summary>
        private void ValidateRequest(CreateOrderRequest request)
        {
            if (request == null)
            {
                throw new ValidationException("Request cannot be null");
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ValidationException("Email is required");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ValidationException("Customer name is required");
            }

            if (string.IsNullOrWhiteSpace(request.Address))
            {
                throw new ValidationException("Shipping address is required");
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                throw new ValidationException("Order must contain at least one item");
            }

            foreach (var item in request.Items)
            {
                if (string.IsNullOrWhiteSpace(item.ProductId))
                {
                    throw new ValidationException("Product ID cannot be empty");
                }

                if (item.Quantity < 1)
                {
                    throw new ValidationException("Item quantity must be at least 1");
                }

                if (item.Price <= 0)
                {
                    throw new ValidationException("Item price must be greater than 0");
                }
            }
        }

        /// <summary>
        /// Checks if customer has placed an order within the duplicate order window
        /// </summary>
        private async Task CheckForDuplicateOrderAsync(string email, CancellationToken cancellationToken)
        {
            try
            {
                var recentOrder = await _orderRepository.GetRecentOrderByEmailAsync(
                    email, 
                    DUPLICATE_ORDER_WINDOW_DAYS, 
                    cancellationToken);

                if (recentOrder != null)
                {
                    _logger.LogInformation(
                        "Customer {Email} placed order within {Days} days. Last order: {OrderId}", 
                        email, DUPLICATE_ORDER_WINDOW_DAYS, recentOrder.Id);
                    // Duplicate orders are allowed, just log for tracking
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for duplicate orders for {Email}", email);
                throw new InvalidOperationException("Failed to validate order history", ex);
            }
        }

        /// <summary>
        /// Converts OrderItemDto list to Order domain model items
        /// </summary>
        private List<OrderItem> ConvertToOrderItems(List<OrderItemDto> itemDtos)
        {
            if (itemDtos == null || itemDtos.Count == 0)
            {
                throw new ValidationException("Items list cannot be empty");
            }

            var items = new List<OrderItem>();
            foreach (var itemDto in itemDtos)
            {
                items.Add(new OrderItem
                {
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = itemDto.Price
                });
            }

            return items;
        }

        /// <summary>
        /// Calculates subtotal, tax, and discount for an order
        /// </summary>
        private (decimal subtotal, decimal tax, decimal discount) CalculatePricing(
            List<OrderItem> items,
            string? discountCode)
        {
            var subtotal = items.Sum(i => i.Quantity * i.UnitPrice);
            var tax = subtotal * TAX_RATE;
            var discount = ParseDiscountCode(discountCode, subtotal);

            return (subtotal, tax, discount);
        }

        /// <summary>
        /// Parses discount code and returns discount amount
        /// </summary>
        private decimal ParseDiscountCode(string? discountCode, decimal subtotal)
        {
            if (string.IsNullOrWhiteSpace(discountCode))
            {
                return 0;
            }

            try
            {
                // Format: PERCENT_XX where XX is percentage (e.g., PERCENT_10 = 10% off)
                if (discountCode.StartsWith("PERCENT_", StringComparison.OrdinalIgnoreCase))
                {
                    var percentPart = discountCode.Substring(8);
                    
                    if (!int.TryParse(percentPart, out var percentValue) || percentValue < 0 || percentValue > 100)
                    {
                        _logger.LogWarning("Invalid discount code format: {DiscountCode}", discountCode);
                        return 0;
                    }

                    var discount = subtotal * (percentValue / 100.0m);
                    return Math.Min(discount, subtotal); // Never discount more than subtotal
                }

                _logger.LogWarning("Unrecognized discount code format: {DiscountCode}", discountCode);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing discount code: {DiscountCode}", discountCode);
                return 0;
            }
        }

        /// <summary>
        /// Validates that sufficient inventory exists
        /// </summary>
        private async Task ValidateInventoryAsync(List<OrderItem> items, CancellationToken cancellationToken)
        {
            try
            {
                var totalQuantity = items.Sum(i => i.Quantity);

                var currentInventoryUsage = await _orderRepository.GetTotalInventoryUsageAsync(cancellationToken);
                var projectedUsage = currentInventoryUsage + totalQuantity;

                if (projectedUsage > INVENTORY_LIMIT)
                {
                    throw new ValidationException(
                        $"Insufficient inventory. Current usage: {currentInventoryUsage}, " +
                        $"requested: {totalQuantity}, limit: {INVENTORY_LIMIT}");
                }
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating inventory");
                throw new InvalidOperationException("Failed to validate inventory", ex);
            }
        }
    }
}
