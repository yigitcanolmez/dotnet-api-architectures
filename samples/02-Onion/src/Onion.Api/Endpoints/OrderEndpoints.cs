using Onion.Application.Dtos;
using Onion.Application.Orders;

namespace Onion.Api.Endpoints;

// [UI halkası] Minimal API endpoint'leri. Sadece Application servisini çağırır;
// Domain kuralı, EF veya SQL bilgisi yok.
public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        group.MapPost("/", async (PlaceOrderRequest request, OrderAppService service, CancellationToken ct) =>
        {
            var id = await service.PlaceAsync(request, ct);
            return Results.Created($"/orders/{id}", new { id });
        });

        group.MapGet("/{id:guid}", async (Guid id, OrderAppService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct)));

        group.MapGet("/", async (OrderAppService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        group.MapPost("/{id:guid}/ship", async (Guid id, OrderAppService service, CancellationToken ct) =>
        {
            await service.ShipAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/cancel", async (Guid id, OrderAppService service, CancellationToken ct) =>
        {
            await service.CancelAsync(id, ct);
            return Results.NoContent();
        });

        return app;
    }
}
