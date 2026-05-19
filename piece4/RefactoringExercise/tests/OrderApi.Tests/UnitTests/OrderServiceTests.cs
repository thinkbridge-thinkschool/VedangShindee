using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderApi.Data;
using OrderApi.Models;
using OrderApi.Services;
using Xunit;

namespace OrderApi.Tests.UnitTests
{
    public class OrderServiceTests
    {
        private OrderDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<OrderDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new OrderDbContext(options);
        }

        private ILogger<OrderService> GetLogger()
        {
            return Mock.Of<ILogger<OrderService>>();
        }

        private HttpClient GetHttpClient()
        {
            return new HttpClient(); // Mocking HttpClient handler is complex, for these tests we'll just test non-HTTP branches or accept it might fail HTTP calls gracefully.
        }

        [Fact]
        public async Task CreateOrderAsync_MissingCustomerDetails_ReturnsFailure()
        {
            // Arrange
            var dbContext = GetDbContext();
            var service = new OrderService(dbContext, GetLogger(), GetHttpClient());
            var request = new OrderRequest { CustomerName = "" };

            // Act
            var result = await service.CreateOrderAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Invalid request", result.Message);
        }

        [Fact]
        public async Task CreateOrderAsync_ProductNotFound_ReturnsFailure()
        {
            // Arrange
            var dbContext = GetDbContext();
            var service = new OrderService(dbContext, GetLogger(), GetHttpClient());
            var request = new OrderRequest
            {
                CustomerName = "John Doe",
                CustomerEmail = "john@example.com",
                CreditCardNumber = "1234567812345678",
                Items = new List<OrderItemRequest>
                {
                    new OrderItemRequest { ProductId = 99, Quantity = 1 }
                }
            };

            // Act
            var result = await service.CreateOrderAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
        }

        [Fact]
        public async Task CreateOrderAsync_ValidRequest_CreatesOrderSuccessfully()
        {
            // Arrange
            var dbContext = GetDbContext();
            dbContext.Products.Add(new Product { Id = 1, Name = "Laptop", Price = 1000m, StockQuantity = 10 });
            await dbContext.SaveChangesAsync();

            var service = new OrderService(dbContext, GetLogger(), GetHttpClient());
            var request = new OrderRequest
            {
                CustomerName = "John Doe",
                CustomerEmail = "john@example.com",
                CreditCardNumber = "1234567812345678",
                Items = new List<OrderItemRequest>
                {
                    new OrderItemRequest { ProductId = 1, Quantity = 1 }
                }
            };

            // Act
            var result = await service.CreateOrderAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.OrderId);
            
            var savedOrder = await dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == result.OrderId);
            Assert.NotNull(savedOrder);
            Assert.Single(savedOrder.Items);
            Assert.Equal(1080m, savedOrder.TotalAmount); // 1000 + 8% tax
        }
    }
}
