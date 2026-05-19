using Moq;
using OrderApi.Interfaces;
using OrderApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OrderApi.Tests.Utilities
{
    /// <summary>
    /// Test utilities for creating mocks and test data
    /// </summary>
    public static class TestDataBuilder
    {
        /// <summary>
        /// Creates a mock order repository
        /// </summary>
        public static Mock<IOrderRepository> CreateMockOrderRepository()
        {
            var mock = new Mock<IOrderRepository>();
            
            // Setup default behavior
            mock.Setup(r => r.AddOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order order, CancellationToken ct) =>
                {
                    order.Id = new Random().Next(1, 10000);
                    return order;
                });

            mock.Setup(r => r.GetRecentOrderByEmailAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string email, int days, CancellationToken ct) => null);

            mock.Setup(r => r.GetTotalInventoryUsageAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            return mock;
        }

        /// <summary>
        /// Creates a valid order creation request for testing
        /// </summary>
        public static Dtos.CreateOrderRequest CreateValidOrderRequest()
        {
            return new Dtos.CreateOrderRequest
            {
                Email = "test@example.com",
                Name = "John Doe",
                Address = "123 Main Street, Springfield, IL 62701",
                Items = new List<Dtos.OrderItemDto>
                {
                    new Dtos.OrderItemDto
                    {
                        ProductId = "PROD-001",
                        Quantity = 2,
                        Price = 29.99m
                    },
                    new Dtos.OrderItemDto
                    {
                        ProductId = "PROD-002",
                        Quantity = 1,
                        Price = 49.99m
                    }
                }
            };
        }

        /// <summary>
        /// Creates a test order with items
        /// </summary>
        public static Order CreateTestOrder()
        {
            return new Order
            {
                Id = 1,
                CustomerName = "Test Customer",
                CustomerEmail = "test@example.com",
                ShippingAddress = "123 Test St",
                TotalAmount = 200.00m,
                Status = "Pending",
                OrderDate = DateTime.UtcNow,
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        ProductId = "TEST-001",
                        Quantity = 2,
                        UnitPrice = 50.00m,
                        OrderId = 1
                    }
                }
            };
        }
    }
}
