using MassTransit;
using Microsoft.EntityFrameworkCore;
using SagaChoreography.Contracts;

namespace SagaChoreography.Services.Ordering;

// [Ordering Servisi — HTTP API] Saga'yı BAŞLATAN tek yer burası (POST /orders). Sonrası tamamen olaylarla ilerler.
public static class OrderingEndpoints
{
    public sealed record PlaceOrderRequest(Guid CustomerId, List<PlaceOrderItem>? Items);
    public sealed record PlaceOrderItem(Guid ProductId, int Quantity);
    public sealed record OrderLineDto(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
    public sealed record OrderDto(Guid Id, Guid CustomerId, string Status, string? StatusReason, decimal Total,
        DateTime CreatedAt, IReadOnlyList<OrderLineDto> Lines);

    public static IEndpointRouteBuilder MapOrderingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products", async (OrderingDbContext db) =>
            Results.Ok((await db.Catalog.AsNoTracking().ToListAsync()).OrderBy(c => c.Name)));

        app.MapPost("/orders", PlaceOrder);
        app.MapGet("/orders", async (OrderingDbContext db) =>
            Results.Ok((await db.Orders.AsNoTracking().OrderByDescending(o => o.CreatedAt).ToListAsync()).Select(ToDto)));
        app.MapGet("/orders/{id:guid}", async (Guid id, OrderingDbContext db) =>
            await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id) is { } order
                ? Results.Ok(ToDto(order))
                : NotFound(id));
        app.MapPost("/orders/{id:guid}/ship", ShipOrder);
        app.MapPost("/orders/{id:guid}/cancel", CancelOrder);
        return app;
    }

    // T1: Siparişi Pending olarak kaydet + OrderCreated yayınla.
    // IPublishEndpoint burada MassTransit "bus outbox"una yazar; mesaj SaveChanges ile AYNI transaction'da
    // OutboxMessage tablosuna girer, arka plandaki delivery servisi sonra bus'a iletir.
    private static async Task<IResult> PlaceOrder(PlaceOrderRequest request, OrderingDbContext db, IPublishEndpoint publisher)
    {
        var items = request.Items ?? [];
        var ids = items.Select(i => i.ProductId).Distinct().ToList();
        var catalog = await db.Catalog.Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        var unknown = ids.Where(id => !catalog.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
            return Results.Problem(title: "Order.UnknownProduct", detail: $"Bilinmeyen ürün: {string.Join(", ", unknown)}", statusCode: 400);

        Order order;
        try
        {
            order = Order.Create(request.CustomerId,
                items.Select(i => new OrderLine(i.ProductId, catalog[i.ProductId].Name, i.Quantity, catalog[i.ProductId].Price)).ToList());
        }
        catch (OrderRuleException ex)
        {
            return Results.Problem(title: "Order.Invalid", detail: ex.Message, statusCode: 400);
        }

        db.Orders.Add(order);
        await publisher.Publish(new OrderCreated(order.Id, order.CustomerId,
            order.Lines.Select(l => new OrderLineItem(l.ProductId, l.Quantity)).ToList(), order.Total));
        await db.SaveChangesAsync(); // Orders + OutboxMessage → tek transaction

        return Results.Created($"/orders/{order.Id}", new { id = order.Id });
    }

    private static async Task<IResult> ShipOrder(Guid id, OrderingDbContext db)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null) return NotFound(id);
        try { order.Ship(); }
        catch (OrderConflictException ex) { return Results.Problem(title: "Order.CannotShip", detail: ex.Message, statusCode: 409); }
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // Onaylanmış siparişin iptali de bir choreography başlatır: OrderCancelled → Inventory stoğu, Payment parayı iade eder.
    private static async Task<IResult> CancelOrder(Guid id, OrderingDbContext db, IPublishEndpoint publisher)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null) return NotFound(id);
        try { order.Cancel("Müşteri iptal etti"); }
        catch (OrderConflictException ex) { return Results.Problem(title: "Order.CannotCancel", detail: ex.Message, statusCode: 409); }

        await publisher.Publish(new OrderCancelled(order.Id, order.StatusReason!));
        await db.SaveChangesAsync(); // Orders + OutboxMessage → tek transaction
        return Results.NoContent();
    }

    private static IResult NotFound(Guid id) =>
        Results.Problem(title: "Order.NotFound", detail: $"Sipariş bulunamadı: {id}", statusCode: 404);

    private static OrderDto ToDto(Order o) => new(o.Id, o.CustomerId, o.Status.ToString(), o.StatusReason, o.Total, o.CreatedAt,
        o.Lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice)).ToList());
}
