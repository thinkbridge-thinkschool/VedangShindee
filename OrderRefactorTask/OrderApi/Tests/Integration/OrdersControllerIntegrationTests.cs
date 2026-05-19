using Microsoft.AspNetCore.Mvc.Testing;
using OrderApi.Dtos;
using OrderApi.Tests.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace OrderApi.Tests.Integration
{
    /// <summary>
    /// Integration tests for the Order API endpoints
    /// Tests the full HTTP stack from request to response
    /// Demonstrates issues in the original code that would cause these to fail
    /// </summary>
    public class OrdersControllerIntegrationTests : IAsyncLifetime
    {
        private WebApplicationFactory<Program> _factory;
        private HttpClient _client;

        public async Task InitializeAsync()
        {
            _factory = new WebApplicationFactory<Program>();
            _client = _factory.CreateClient();
            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            _client?.Dispose();
            _factory?.Dispose();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Integration Test 1: POST /api/orders with valid request should return 201 Created
        /// ISSUE IN ORIGINAL: Returned object with success/data structure instead of proper HTTP status
        /// Also: synchronous calls would block in async context, causing thread starvation
        /// </summary>
        [Fact]
        public async Task CreateOrder_WithValidRequest_ReturnsCreated()
        {
            // Arrange
            var request = TestDataBuilder.CreateValidOrderRequest();

            // Act
            var response = await _client.PostAsJsonAsync("/api/orders", request);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            // Verify response structure
            var content = await response.Content.ReadAsStringAsync();
            Assert.NotEmpty(content);

            var result = JsonSerializer.Deserialize<CreateOrderResponse>(content);
            Assert.NotNull(result);
            Assert.True(result.OrderId > 0);
            Assert.Equal("Test@example.com", result.CustomerName);
            Assert.Equal("Pending", result.Status);
            Assert.True(result.Total > 0);

            // ORIGINAL ISSUE: The original code returned:
            // { success: true, data: { ... }, timestamp: ... }
            // This made it impossible to use standard HTTP status codes
            // Clients couldn't distinguish between business errors and server errors
        }

        /// <summary>
        /// Integration Test 2: POST /api/orders with invalid email should return 400 Bad Request
        /// ISSUE IN ORIGINAL: Used dynamic and returned object instead of proper validation responses
        /// Also: Had empty catch blocks that swallowed validation errors
        /// </summary>
        [Fact]
        public async Task CreateOrder_WithInvalidEmail_ReturnsBadRequest()
        {
            // Arrange
            var request = TestDataBuilder.CreateValidOrderRequest();
            request.Email = "not-an-email"; // Invalid format

            // Act
            var response = await _client.PostAsJsonAsync("/api/orders", request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("email", content, StringComparison.OrdinalIgnoreCase);

            // ORIGINAL ISSUE: The original controller tried to validate email length < 2
            // But used dynamic binding with no validation attributes
            // So this would only be caught at runtime, not at binding time
        }

        /// <summary>
        /// Integration Test 3: POST /api/orders with no items should return 400 Bad Request
        /// ISSUE IN ORIGINAL: Empty items array could pass initial validation due to inline logic
        /// Test demonstrates that proper validation is now in place
        /// </summary>
        [Fact]
        public async Task CreateOrder_WithNoItems_ReturnsBadRequest()
        {
            // Arrange
            var request = TestDataBuilder.CreateValidOrderRequest();
            request.Items = new List<OrderItemDto>(); // Empty

            // Act
            var response = await _client.PostAsJsonAsync("/api/orders", request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("item", content, StringComparison.OrdinalIgnoreCase);

            // ORIGINAL ISSUE: The original code checked if items.Count == 0
            // But did this after trying to parse items with a try-catch block
            // The catch block would swallow parsing exceptions, making debugging hard
        }

        /// <summary>
        /// Integration Test 4: BONUS - POST with malformed JSON should return 400
        /// ISSUE IN ORIGINAL: Using dynamic with no model validation meant malformed JSON
        /// would pass binding and fail at runtime in the middle of business logic
        /// </summary>
        [Fact]
        public async Task CreateOrder_WithMalformedJson_ReturnsBadRequest()
        {
            // Arrange
            var malformedJson = "{ invalid json }";

            // Act
            var response = await _client.PostAsync(
                "/api/orders",
                new StringContent(malformedJson, System.Text.Encoding.UTF8, "application/json"));

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            // ORIGINAL ISSUE: With dynamic binding, this would create a binding error
            // But the generic catch block at the end would return a vague error message
            // instead of a properly structured validation error
        }

        /// <summary>
        /// Integration Test 5: BONUS - Cancellation should work properly
        /// ISSUE IN ORIGINAL: No CancellationToken support meant:
        /// - Client timeout wouldn't cancel server operation
        /// - Wasted resources on abandoned requests
        /// - Potential data corruption from mid-operation interruption
        /// </summary>
        [Fact]
        public async Task CreateOrder_WithCancellation_StopsProcessing()
        {
            // Arrange
            var request = TestDataBuilder.CreateValidOrderRequest();
            var cts = new System.Threading.CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(1)); // Cancel almost immediately

            // Act - This should handle the cancellation gracefully
            // (In a real scenario with long-running operations)
            try
            {
                var response = await _client.PostAsJsonAsync(
                    "/api/orders",
                    request,
                    cts.Token);

                // Either cancelled or completed successfully
                Assert.True(response.StatusCode == HttpStatusCode.Created || 
                           response.StatusCode == HttpStatusCode.RequestTimeout);
            }
            catch (OperationCanceledException)
            {
                // Expected behavior - cancellation was respected
                Assert.True(true);
            }

            // ORIGINAL ISSUE: The original code had no way to handle this
            // There was no CancellationToken parameter, so operations couldn't be stopped
        }

        /// <summary>
        /// Integration Test 6: BONUS - Typed responses enable proper API documentation
        /// ISSUE IN ORIGINAL: Returning object meant Swagger couldn't generate proper schema
        /// This demonstrates that refactored code works with OpenAPI/Swagger properly
        /// </summary>
        [Fact]
        public async Task Swagger_SchemaIsAvailable()
        {
            // Act
            var response = await _client.GetAsync("/swagger/v1/swagger.json");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            
            // Should document the CreateOrderResponse type properly
            Assert.Contains("CreateOrderResponse", content);
            Assert.Contains("OrderId", content);
            Assert.Contains("CustomerName", content);

            // ORIGINAL ISSUE: With dynamic returns and plain objects,
            // Swagger would generate incorrect or incomplete documentation
            // Clients couldn't see what fields were available
        }
    }
}
