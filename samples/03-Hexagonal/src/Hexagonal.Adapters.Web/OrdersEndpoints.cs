using Hexagonal.Adapters.Web.Contracts;
using Hexagonal.Core.Ports.In;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexagonal.Adapters.Web;

// [Driving Adapter: HTTP] HTTP isteğini inbound port çağrısına çevirir. SADECE Ports/In arayüzlerini bilir;
// use case implementasyonlarını, repository'yi veya EF'i görmez.
public static class OrdersEndpoints
{
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        group.MapPost("/", async (PlaceOrderRequest request, IPlaceOrderUseCase placeOrder, CancellationToken ct) =>
        {
            var command = new PlaceOrderCommand(
                request.CustomerId,
                (request.Items ?? []).Select(i => new PlaceOrderItem(i.ProductId, i.Quantity)).ToList());

            var id = await placeOrder.ExecuteAsync(command, ct);
            return Results.Created($"/orders/{id}", new { id });
        });

        group.MapGet("/{id:guid}", async (Guid id, IGetOrderQuery query, CancellationToken ct) =>
            await query.GetAsync(id, ct) is { } order
                ? Results.Ok(order)
                : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Kayıt bulunamadı", detail: $"Sipariş bulunamadı: {id}"));

        group.MapGet("/", async (IGetOrderQuery query, CancellationToken ct) =>
            Results.Ok(await query.ListAsync(ct)));

        group.MapPost("/{id:guid}/ship", async (Guid id, IShipOrderUseCase shipOrder, CancellationToken ct) =>
        {
            await shipOrder.ExecuteAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/cancel", async (Guid id, ICancelOrderUseCase cancelOrder, CancellationToken ct) =>
        {
            await cancelOrder.ExecuteAsync(id, ct);
            return Results.NoContent();
        });

        return app;
    }
}
