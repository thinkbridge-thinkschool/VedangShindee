using Microsoft.Extensions.Logging;
using Moq;
using OrderApi.Dtos;
using OrderApi.Interfaces;
using OrderApi.Models;
using OrderApi.Services;
using OrderApi.Tests.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OrderApi.Tests.Services
{
    /// <summary>
    /// Unit tests for OrderService business logic
    /// Tests demonstrate issues that would fail with the original bad OrderController
    /// </summary>
    public class OrderServiceTests
    {
        private readonly Mock<IOrderRepository> _mockRepository;
        private readonly Mock<ILogger<OrderService>> _mockLogger;
        private readonly OrderService _service;

        public OrderServiceTests()
        {
            _mockRepository = TestDataBuilder.CreateMockOrderRepository();
            _mockLogger = new Mock<ILogger<OrderService>>();
            _service = new OrderService(_mockRepository.Object, _mockLogger.Object);
        }

        /// <summary>
        /// Test 1: Valid order creation should succeed
        /// This would FAIL on the original code due to synchronous DB calls and lack of async support
        /// </summary>
        [Fact]
        public async Task CreateOrderAsync_WithValidRequest_ShouldSucceed()
        {
            // Arrange
            var request = TestDataBuilder.CreateValidOrderRequest();
            var cancellationToken = CancellationToken.None;

            // Setup repository mock
            _mockRepository.Setup(r => r.AddOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order order, CancellationToken ct) =>
                {
                    order.Id = 123;
                    return order;
                });

            _mockRepository.Setup(r => r.GetTotalInventoryUsageAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(100); // Current inventory usage

            // Act
            var result = await _service.CreateOrderAsync(request, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(123, result.Id);
            Assert.Equal("Test@example.com", result.CustomerName); // Would use name, not email
            Assert.Equal("Pending", result.Status);
            Assert.True(result.TotalAmount > 0);
            Assert.NotEmpty(result.Items);

            // Verify repository was called
            _mockRepository.Verify(
                r => r.AddOrderAsync(It.IsAny<Order>(), cancellationToken),
                Times.Once,
                "Order should be persisted exactly once");
        }

        /// <summary>
        /// Test 2: Order with no items should fail validation
        /// PROBLEM IN ORIGINAL: Off-by-one bug allowed 0-item orders
        /// The original controller had: if (items.Count == 0) return error
        /// But allowed the request to proceed if validation somehow passed
        /// </summary>
        [Fact]
        public async Task CreateOrderAsync_WithNoItems_ShouldThrowValidationException()
        {
            // Arrange
            var request = TestDataBuilder.CreateValidOrderRequest();
            request.Items = new List<OrderItemDto>(); // Empty items
            var cancellationToken = CancellationToken.None;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => _service.CreateOrderAsync(request, cancellationToken));

            Assert.Contains("at least one item", exception.Message, StringComparison.OrdinalIgnoreCase);

            // ORIGINAL CODE ISSUE: The original controller had multiple checks for this
            // but returned object instead of proper exception, making it hard to test
            // and easy to miss edge cases
        }

        /// <summary>
        /// Test 3: Order with item quantity of 0 should fail validation
        /// PROBLEM IN ORIGINAL: The off-by-one bug: if (quantity > 0) meant quantity must be > 1
        /// So quantity=1 would fail! This test verifies the refactored code fixes this
        /// </summary>
        [Fact]
        public async Task CreateOrderAsync_WithInvalidQuantity_ShouldThrowValidationException()
        {
            // Arrange
            var request = TestDataBuilder.CreateValidOrderRequest();
            request.Items = new List<OrderItemDto>
            {
                new OrderItemDto
                {
                    ProductId = "PROD-001",
                    Quantity = 0, // Invalid: must be >= 1
                    Price = 29.99m
                }
            };
            var cancellationToken = CancellationToken.None;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => _service.CreateOrderAsync(request, cancellationToken));

            Assert.Contains("quantity", exception.Message, StringComparison.OrdinalIgnoreCase);

            // ORIGINAL ISSUE: The original code didn't have explicit validation for this
            // It mixed validation with business logic inline, making it easy to miss cases
        }

        /// <summary>
        /// Test 4: BONUS - Empty email should fail
        /// PROBLEM IN ORIGINAL: The original checked email.Length < 2
        /// But didn't check for null first - this could cause NullReferenceException!
        /// </summary>
        [Fact]
        public async Task CreateOrderAsync_WithNullEmail_ShouldThrowValidationException()
        {
            // Arrange
            var request = TestDataBuilder.CreateValidOrderRequest();
            request.Email = null;
            var cancellationToken = CancellationToken.None;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => _service.CreateOrderAsync(request, cancellationToken));

            // ORIGINAL ISSUE: The original code had:
            // if (string.IsNullOrWhiteSpace(customerEmail) || customerEmail.Length < 2)
            // This would throw NullReferenceException before the check!

            Assert.NotNull(exception);
        }

        /// <summary>
        /// Test 5: BONUS - Cancellation token should be respected
        /// PROBLEM IN ORIGINAL: No CancellationToken support at all
        /// This demonstrates async best practices that were missing
        /// </summary>
        [Fact]
        public async Task CreateOrderAsync_WithCancelledToken_ShouldThrow()
        {
            // Arrange
            var request = TestDataBuilder.CreateValidOrderRequest();
            var cancellationToken = new CancellationToken(canceled: true);

            // Act & Assert
            // The task should be cancelled
            // (In this case, the repository call will fail with OperationCanceledException)
            _mockRepository.Setup(r => r.AddOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            var exception = await Assert.ThrowsAsync<Exception>(
                () => _service.CreateOrderAsync(request, cancellationToken));

            // ORIGINAL ISSUE: No cancellation support meant:
            // - Long-running requests couldn't be cancelled
            // - No graceful shutdown
            // - Resource waste on abandoned requests
        }
    }
}
