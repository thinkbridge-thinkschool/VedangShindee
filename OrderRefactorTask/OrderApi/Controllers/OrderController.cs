using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrderApi.Dtos;
using OrderApi.Services;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace OrderApi.Controllers
{
    /// <summary>
    /// Controller for order management endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
        {
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new order
        /// </summary>
        /// <param name="request">The order creation request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The created order response</returns>
        [HttpPost]
        [ProducesResponseType(typeof(CreateOrderResponse), 201)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 500)]
        public async Task<ActionResult<CreateOrderResponse>> CreateOrder(
            [FromBody] CreateOrderRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var order = await _orderService.CreateOrderAsync(request, cancellationToken);

                var response = new CreateOrderResponse
                {
                    OrderId = order.Id,
                    CustomerName = order.CustomerName,
                    CustomerEmail = order.CustomerEmail,
                    ItemCount = order.Items.Count,
                    Subtotal = order.Items.Sum(i => i.Quantity * i.UnitPrice),
                    Tax = order.Items.Sum(i => i.Quantity * i.UnitPrice) * 0.10m,
                    Discount = 0, // This should be calculated in service if needed
                    Total = order.TotalAmount,
                    Status = order.Status,
                    OrderDate = order.OrderDate,
                    EstimatedDelivery = order.OrderDate.AddDays(5)
                };

                return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, response);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "Validation error during order creation");
                return BadRequest(new ErrorResponse
                {
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during order creation");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An unexpected error occurred. Please try again."
                });
            }
        }

        /// <summary>
        /// Retrieves an order by ID
        /// </summary>
        /// <param name="id">The order ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The order response</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(OrderResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        public async Task<ActionResult<OrderResponse>> GetOrder(
            int id,
            CancellationToken cancellationToken)
        {
            try
            {
                // This would require adding a GetOrder method to the service
                // For now, returning NotFound
                return NotFound(new ErrorResponse
                {
                    Message = "Order not found"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order {OrderId}", id);
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An unexpected error occurred"
                });
            }
        }
    }
}

