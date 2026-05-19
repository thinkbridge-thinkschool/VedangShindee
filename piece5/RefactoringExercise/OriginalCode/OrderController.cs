using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace RefactoringExercise.OriginalCode
{
    // Deliberately bad file combining Entities, DTOs, DbContext, and a monolithic controller
    
    public class OrderRequest
    {
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string ShippingAddress { get; set; }
        public string CreditCardNumber { get; set; }
        public string ExpiryDate { get; set; }
        public string Cvv { get; set; }
        public List<OrderItemRequest> Items { get; set; }
        public string DiscountCode { get; set; }
        public string Notes { get; set; }
    }

    public class OrderItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
    }

    public class Order
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
    }

    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Simple mapping
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public OrdersController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost]
        [Route("create-order")]
        public async Task<object> CreateOrder([FromBody] OrderRequest request)
        {
            // 1. Log request start to a file. 
            // Anti-pattern: Empty catch block swallowing file I/O exceptions
            try
            {
                System.IO.File.AppendAllText("order_requests_log.txt", $"[{DateTime.UtcNow}] Order started for {request.CustomerName}\n");
            }
            catch { }

            // 2. Inline validation logic (Should be using FluentValidation or DataAnnotations)
            if (request == null)
            {
                return new { success = false, message = "Request is null" };
            }
            if (string.IsNullOrEmpty(request.CustomerName))
            {
                return new { success = false, message = "CustomerName is required" };
            }
            if (string.IsNullOrEmpty(request.CustomerEmail) || !request.CustomerEmail.Contains("@"))
            {
                return new { success = false, message = "Invalid email" };
            }
            if (request.Items == null || request.Items.Count == 0)
            {
                return new { success = false, message = "No items in order" };
            }
            if (string.IsNullOrEmpty(request.CreditCardNumber) || request.CreditCardNumber.Length < 16)
            {
                return new { success = false, message = "Invalid Credit Card" };
            }

            // Anti-pattern: Synchronous EF call inside an async action
            var userHasPendingOrders = _dbContext.Orders.Where(o => o.CustomerEmail == request.CustomerEmail && o.Status == "Pending").ToList();
            if (userHasPendingOrders.Count > 3)
            {
                return new { success = false, message = "Too many pending orders" };
            }

            decimal totalAmount = 0;
            var orderItemsToSave = new List<OrderItem>();

            // 3. Business logic mixed with data access
            // Anti-pattern: Off-by-one error (<= instead of <)
            for (int i = 0; i <= request.Items.Count; i++)
            {
                var item = request.Items[i]; // This will throw ArgumentOutOfRangeException when i == request.Items.Count

                // Anti-pattern: Synchronous database call in a loop
                var product = _dbContext.Products.FirstOrDefault(p => p.Id == item.ProductId);
                
                // Anti-pattern: Null reference dereference. 
                // If product is not found, product is null. We access .Price without checking!
                decimal itemPrice = product.Price; 

                if (product.StockQuantity < item.Quantity)
                {
                    return new { success = false, message = $"Insufficient stock for product {product.Name}" };
                }

                // Deduct stock (synchronously modifying entity)
                product.StockQuantity -= item.Quantity;

                var orderItem = new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = itemPrice
                };

                orderItemsToSave.Add(orderItem);
                totalAmount += (itemPrice * item.Quantity);
            }

            // 4. More inline business logic - Applying discounts
            if (!string.IsNullOrEmpty(request.DiscountCode))
            {
                if (request.DiscountCode == "SAVE10")
                {
                    totalAmount = totalAmount * 0.9m;
                }
                else if (request.DiscountCode == "SAVE20")
                {
                    totalAmount = totalAmount * 0.8m;
                }
                else
                {
                    // Maybe we want to call an external API to check discount codes?
                    try
                    {
                        var httpClient = new HttpClient();
                        var result = httpClient.GetAsync($"https://api.discount-checker.com/validate?code={request.DiscountCode}").Result; // Anti-pattern: sync over async
                        if (result.IsSuccessStatusCode)
                        {
                            var content = result.Content.ReadAsStringAsync().Result;
                            if (content.Contains("valid"))
                            {
                                totalAmount -= 5.0m;
                            }
                        }
                    }
                    catch { } // Anti-pattern: Empty catch swallowing network exceptions
                }
            }

            // 5. Taxation calculation inline
            decimal taxRate = 0.08m; // Hardcoded tax
            if (request.ShippingAddress.Contains("NY") || request.ShippingAddress.Contains("New York"))
            {
                taxRate = 0.08875m;
            }
            else if (request.ShippingAddress.Contains("CA") || request.ShippingAddress.Contains("California"))
            {
                taxRate = 0.0725m;
            }
            decimal totalWithTax = totalAmount + (totalAmount * taxRate);

            // 6. External Payment Gateway processing inline
            bool paymentSuccess = false;
            try
            {
                // Fake payment processing
                var paymentClient = new HttpClient();
                var paymentPayload = new StringContent(JsonSerializer.Serialize(new { 
                    cc = request.CreditCardNumber, 
                    exp = request.ExpiryDate, 
                    cvv = request.Cvv, 
                    amount = totalWithTax 
                }), Encoding.UTF8, "application/json");

                // Anti-pattern: Sync over async
                var paymentResponse = paymentClient.PostAsync("https://fake-payment-gateway.com/charge", paymentPayload).Result;
                if (paymentResponse.IsSuccessStatusCode)
                {
                    paymentSuccess = true;
                }
            }
            catch { } // Anti-pattern: Empty catch swallowing payment failure exceptions and proceeding silently

            if (!paymentSuccess)
            {
                // Wait, if it fails because of exception, we just swallow it and paymentSuccess remains false.
                // It might return an unhelpful message or we might just assume success if we didn't check.
                // Let's pretend we just log and continue for some reason? No, let's reject.
                return new { success = false, message = "Payment failed" };
            }

            // 7. Data Persistence
            var newOrder = new Order
            {
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                TotalAmount = totalWithTax,
                OrderDate = DateTime.UtcNow,
                Status = "Paid",
                Items = orderItemsToSave
            };

            // Anti-pattern: Synchronous Add and SaveChanges in an async method
            _dbContext.Orders.Add(newOrder);
            _dbContext.SaveChanges();

            // Assign foreign keys since we didn't properly use navigation properties setup
            foreach (var item in orderItemsToSave)
            {
                item.OrderId = newOrder.Id;
                _dbContext.OrderItems.Add(item);
            }
            // Anti-pattern: another synchronous SaveChanges
            _dbContext.SaveChanges();

            // 8. Send Confirmation Email inline
            try
            {
                // Simulating an SMTP client
                var smtpClient = new System.Net.Mail.SmtpClient("smtp.fake-mail-server.com");
                smtpClient.Port = 587;
                var mailMessage = new System.Net.Mail.MailMessage("noreply@ourstore.com", request.CustomerEmail);
                mailMessage.Subject = $"Order Confirmation #{newOrder.Id}";
                mailMessage.Body = $"Thank you for your order! Total amount: {totalWithTax}";
                
                // Anti-pattern: Sync network call
                smtpClient.Send(mailMessage);
            }
            catch { } // Anti-pattern: Empty catch swallowing email sending exceptions

            // 9. Return an anonymous object instead of a strongly typed HTTP response (like CreatedAtAction or Ok(dto))
            return new
            {
                success = true,
                message = "Order placed successfully",
                orderId = newOrder.Id,
                totalPaid = totalWithTax,
                estimatedDelivery = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd")
            };
        }
    }
}
