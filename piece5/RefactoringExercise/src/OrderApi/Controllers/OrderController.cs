using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OrderApi.Models;
using OrderApi.Services;

namespace OrderApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost]
        [Route("create-order")]
        public async Task<ActionResult<OrderResponse>> CreateOrder([FromBody] OrderRequest request, CancellationToken cancellationToken)
        {
            var response = await _orderService.CreateOrderAsync(request, cancellationToken);
            
            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
    }
}
