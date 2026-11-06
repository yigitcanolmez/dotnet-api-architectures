using Clean.Application.Orders.Commands.CancelOrder;
using Clean.Application.Orders.Commands.PlaceOrder;
using Clean.Application.Orders.Commands.ShipOrder;
using Clean.Application.Orders.Queries;
using Clean.Application.Orders.Queries.GetOrderById;
using Clean.Application.Orders.Queries.ListOrders;
using MediatR;

namespace Clean.Api.Endpoints;

// [Api Katmanı – Interface Adapters] "İnce" endpoint'ler: HTTP → Command/Query → ISender → HTTP yanıtı.
// Hata durumları burada if/else ile değil, GlobalExceptionHandler'da merkezi olarak ProblemDetails'e çevrilir.
public static class OrderEndpoints
{
    public sealed record ShipOrderRequest(string? TrackingNumber);
    public sealed record CancelOrderRequest(string? Reason);

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        group.MapPost("/", async (PlaceOrderCommand command, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/orders/{id}", new { id });
        });

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListOrdersQuery(), ct)));

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetOrderByIdQuery(id), ct)))
            .Produces<OrderDto>();

        // Gövde opsiyonel: takip no verilmezse örnek bir numara üretilir.
        group.MapPost("/{id:guid}/ship", async (Guid id, ShipOrderRequest? body, ISender sender, CancellationToken ct) =>
        {
            var trackingNumber = string.IsNullOrWhiteSpace(body?.TrackingNumber)
                ? $"TRK-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"
                : body.TrackingNumber;
            await sender.Send(new ShipOrderCommand(id, trackingNumber), ct);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/cancel", async (Guid id, CancelOrderRequest? body, ISender sender, CancellationToken ct) =>
        {
            var reason = string.IsNullOrWhiteSpace(body?.Reason) ? "Müşteri talebi" : body.Reason;
            await sender.Send(new CancelOrderCommand(id, reason), ct);
            return Results.NoContent();
        });

        return app;
    }
}
