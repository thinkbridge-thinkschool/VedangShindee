using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderApi.Data;
using OrderApi.Models;

namespace OrderApi.Services
{
    public interface IOrderService
    {
        Task<OrderResponse> CreateOrderAsync(OrderRequest request, CancellationToken cancellationToken = default);
    }

    public class OrderService : IOrderService
    {
        private readonly OrderDbContext _dbContext;
        private readonly ILogger<OrderService> _logger;
        private readonly HttpClient _httpClient;

        public OrderService(OrderDbContext dbContext, ILogger<OrderService> logger, HttpClient httpClient)
        {
            _dbContext = dbContext;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<OrderResponse> CreateOrderAsync(OrderRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Order started for {CustomerName}", request.CustomerName);

            // Validation (simplified for exercise, normally use FluentValidation)
            if (request == null || string.IsNullOrEmpty(request.CustomerName) || string.IsNullOrEmpty(request.CustomerEmail))
            {
                return new OrderResponse { Success = false, Message = "Invalid request or missing customer details." };
            }

            if (request.Items == null || !request.Items.Any())
            {
                return new OrderResponse { Success = false, Message = "No items in order." };
            }

            // Async EF Call
            var pendingOrdersCount = await _dbContext.Orders
                .CountAsync(o => o.CustomerEmail == request.CustomerEmail && o.Status == "Pending", cancellationToken);

            if (pendingOrdersCount > 3)
            {
                return new OrderResponse { Success = false, Message = "Too many pending orders." };
            }

            decimal totalAmount = 0;
            var orderItemsToSave = new List<OrderItem>();

            // Fix off-by-one by using foreach
            foreach (var item in request.Items)
            {
                var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken);
                
                // Fix Null Reference Dereference
                if (product == null)
                {
                    return new OrderResponse { Success = false, Message = $"Product {item.ProductId} not found." };
                }

                if (product.StockQuantity < item.Quantity)
                {
                    return new OrderResponse { Success = false, Message = $"Insufficient stock for product {product.Name}." };
                }

                product.StockQuantity -= item.Quantity;

                orderItemsToSave.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                });

                totalAmount += (product.Price * item.Quantity);
            }

            // Apply discount
            if (!string.IsNullOrEmpty(request.DiscountCode))
            {
                if (request.DiscountCode == "SAVE10") totalAmount *= 0.9m;
                else if (request.DiscountCode == "SAVE20") totalAmount *= 0.8m;
                else
                {
                    try
                    {
                        // Fix sync-over-async and specific exception catching
                        var response = await _httpClient.GetAsync($"https://api.discount-checker.com/validate?code={request.DiscountCode}", cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync(cancellationToken);
                            if (content.Contains("valid"))
                            {
                                totalAmount -= 5.0m;
                            }
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogWarning(ex, "Failed to validate discount code {Code}. Ignoring discount.", request.DiscountCode);
                    }
                }
            }

            // Tax
            decimal taxRate = 0.08m;
            if (request.ShippingAddress.Contains("NY")) taxRate = 0.08875m;
            else if (request.ShippingAddress.Contains("CA")) taxRate = 0.0725m;
            
            decimal totalWithTax = totalAmount + (totalAmount * taxRate);

            // Payment Processing (Simulated)
            bool paymentSuccess = SimulatePayment(request.CreditCardNumber);
            if (!paymentSuccess)
            {
                return new OrderResponse { Success = false, Message = "Payment failed." };
            }

            var newOrder = new Order
            {
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                TotalAmount = totalWithTax,
                OrderDate = DateTime.UtcNow,
                Status = "Paid",
                Items = orderItemsToSave
            };

            _dbContext.Orders.Add(newOrder);

            // Fix multiple SaveChanges
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Email Sending
            try
            {
                await SendConfirmationEmailAsync(newOrder.CustomerEmail, newOrder.Id, totalWithTax);
            }
            catch (Exception ex)
            {
                // Narrow exception, log and continue since order is paid and saved
                _logger.LogWarning(ex, "Failed to send confirmation email to {Email}", newOrder.CustomerEmail);
            }

            return new OrderResponse
            {
                Success = true,
                Message = "Order placed successfully",
                OrderId = newOrder.Id,
                TotalPaid = totalWithTax,
                EstimatedDelivery = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd")
            };
        }

        private bool SimulatePayment(string cc)
        {
            // Simulate a successful payment if CC length is >= 16
            return cc.Length >= 16;
        }

        private Task SendConfirmationEmailAsync(string email, int orderId, decimal amount)
        {
            // Simulate email sending
            _logger.LogInformation("Sending email for order {OrderId} to {Email}", orderId, email);
            return Task.CompletedTask;
        }
    }
}
