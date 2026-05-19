using Microsoft.AspNetCore.Mvc;
using OrderApi.Interfaces;
using OrderApi.Models;

namespace OrderApi.Controllers;

[ApiController]
[Route("api/orders")]
public class TestController : ControllerBase
{
    private readonly IOrderRepository _repository;

    public TestController(IOrderRepository repository)
    {
        _repository = repository;
    }

    [HttpPost]
    public async Task<IActionResult> Create(Order order)
    {
        await _repository.AddAsync(order, CancellationToken.None);

        return Ok(order);
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok("API Working");
    }
}