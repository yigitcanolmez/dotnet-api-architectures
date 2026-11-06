using Cqrs.Api.Common;
using Cqrs.Api.Write.Commands;

namespace Cqrs.Api.Endpoints;

// [API / Komut tarafı] Komut endpoint'leri sadece ID veya 204 döner. Güncel veriyi görmek
// isteyen istemci okuma endpoint'ine gider (ve kısa bir süre eski veri görebilir).
public static class CommandEndpoints
{
    public sealed record PlaceOrderResponse(Guid Id);

    public static IEndpointRouteBuilder MapCommandEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders").WithTags("Commands");

        orders.MapPost("/", async (PlaceOrderCommand command, ICommandHandler<PlaceOrderCommand, Guid> handler, CancellationToken ct) =>
        {
            var id = await handler.HandleAsync(command, ct);
            return Results.Created($"/orders/{id}", new PlaceOrderResponse(id));
        });

        orders.MapPost("/{id:guid}/ship", async (Guid id, ICommandHandler<ShipOrderCommand> handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new ShipOrderCommand(id), ct);
            return Results.NoContent();
        });

        orders.MapPost("/{id:guid}/cancel", async (Guid id, ICommandHandler<CancelOrderCommand> handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new CancelOrderCommand(id), ct);
            return Results.NoContent();
        });

        return app;
    }
}
