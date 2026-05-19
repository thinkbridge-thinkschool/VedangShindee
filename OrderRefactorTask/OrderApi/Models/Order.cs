using System;
using System.Collections.Generic;

namespace OrderApi.Models;

/// <summary>
/// Domain model representing a customer order
/// </summary>
public class Order
{
    public int Id { get; set; }

    /// <summary>
    /// Customer's name
    /// </summary>
    public string CustomerName { get; set; } = "";

    /// <summary>
    /// Customer's email address
    /// </summary>
    public string CustomerEmail { get; set; } = "";

    /// <summary>
    /// Shipping address for the order
    /// </summary>
    public string ShippingAddress { get; set; } = "";

    /// <summary>
    /// Total amount including tax and discounts
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Order status (e.g., Pending, Processing, Shipped, Delivered)
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// When the order was created (UTC)
    /// </summary>
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Items in the order
    /// </summary>
    public List<OrderItem> Items { get; set; } = new List<OrderItem>();
}

/// <summary>
/// Domain model representing a line item in an order
/// </summary>
public class OrderItem
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the Order
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// The product being ordered
    /// </summary>
    public string ProductId { get; set; } = "";

    /// <summary>
    /// Quantity ordered
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Unit price at time of order
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Navigation property to Order
    /// </summary>
    public Order? Order { get; set; }
}
