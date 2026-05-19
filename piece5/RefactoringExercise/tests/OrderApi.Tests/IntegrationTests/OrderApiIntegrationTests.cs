using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrderApi.Data;
using OrderApi.Models;
using Xunit;

namespace OrderApi.Tests.IntegrationTests
{
    public class OrderApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public OrderApiIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task PostCreateOrder_ReturnsSuccess()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Seed data
                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                    db.Products.Add(new Product { Id = 2, Name = "Mouse", Price = 50m, StockQuantity = 100 });
                    db.SaveChanges();
                });
            }).CreateClient();

            var request = new OrderRequest
            {
                CustomerName = "Jane Doe",
                CustomerEmail = "jane@example.com",
                CreditCardNumber = "1111222233334444", // >= 16 chars for success
                ShippingAddress = "123 Main St",
                Items = new System.Collections.Generic.List<OrderItemRequest>
                {
                    new OrderItemRequest { ProductId = 2, Quantity = 2 }
                }
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/orders/create-order", request);

            // Assert
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OrderResponse>();
            
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(result.OrderId > 0);
            Assert.Equal(108m, result.TotalPaid); // 50 * 2 = 100 + 8% tax
        }
    }
}
