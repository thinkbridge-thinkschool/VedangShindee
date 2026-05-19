using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OrderApi.Dtos
{
    /// <summary>
    /// Request DTO for creating a new order
    /// </summary>
    public class CreateOrderRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Customer name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Shipping address is required")]
        [StringLength(200, MinimumLength = 5, ErrorMessage = "Address must be between 5 and 200 characters")]
        public string Address { get; set; }

        [Required(ErrorMessage = "At least one item is required")]
        [MinLength(1, ErrorMessage = "Order must contain at least one item")]
        public List<OrderItemDto> Items { get; set; }

        [StringLength(50, ErrorMessage = "Discount code must be 50 characters or less")]
        public string? DiscountCode { get; set; }
    }

    /// <summary>
    /// DTO for order items in request
    /// </summary>
    public class OrderItemDto
    {
        [Required(ErrorMessage = "Product ID is required")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "Product ID must be between 1 and 50 characters")]
        public string ProductId { get; set; }

        [Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000")]
        public int Quantity { get; set; }

        [Range(0.01, 999999.99, ErrorMessage = "Price must be between 0.01 and 999,999.99")]
        public decimal Price { get; set; }
    }

    /// <summary>
    /// Response DTO for created order
    /// </summary>
    public class CreateOrderResponse
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public int ItemCount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime EstimatedDelivery { get; set; }
    }

    /// <summary>
    /// Response DTO for retrieving an order
    /// </summary>
    public class OrderResponse
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string ShippingAddress { get; set; }
        public List<OrderItemResponse> Items { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public DateTime OrderDate { get; set; }
    }

    /// <summary>
    /// DTO for order items in response
    /// </summary>
    public class OrderItemResponse
    {
        public string ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Quantity * UnitPrice;
    }

    /// <summary>
    /// Error response DTO
    /// </summary>
    public class ErrorResponse
    {
        public bool Success { get; set; } = false;
        public string Message { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
