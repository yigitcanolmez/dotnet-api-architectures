using Layered.Business.Dtos;
using Layered.Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace Layered.Api.Controllers;

// [Presentation Katmanı] Klasik [ApiController]. Sadece HTTP ↔ servis çevirisi yapar.
[ApiController]
[Route("orders")]
public class OrdersController(OrderService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Place(PlaceOrderRequest request, CancellationToken ct)
    {
        var id = await service.PlaceAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken ct) =>
        await service.GetAsync(id, ct);

    [HttpGet]
    public Task<IReadOnlyList<OrderDto>> List(CancellationToken ct) => service.ListAsync(ct);

    [HttpPost("{id:guid}/ship")]
    public async Task<IActionResult> Ship(Guid id, CancellationToken ct)
    {
        await service.ShipAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await service.CancelAsync(id, ct);
        return NoContent();
    }
}
