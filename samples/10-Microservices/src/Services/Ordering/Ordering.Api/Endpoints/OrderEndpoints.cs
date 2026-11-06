// [Ordering — API] Minimal API uçları. POST /orders, Transactional Outbox'ın kalbidir.
using Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Domain;
using Ordering.Api.Infrastructure;

namespace Ordering.Api.Endpoints;

public record PlaceOrderItem(Guid ProductId, int Quantity);
public record PlaceOrderRequest(Guid CustomerId, List<PlaceOrderItem>? Items);

public record OrderLineDto(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record OrderDto(Guid Id, Guid CustomerId, string Status, decimal Total, string? RejectionReason,
    DateTime CreatedAt, IReadOnlyList<OrderLineDto> Lines);
public record SagaDto(Guid OrderId, string CurrentState, decimal Total, Guid? PaymentId, string? FailureReason,
    DateTime SubmittedAt, DateTime? UpdatedAt, int Version);

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders");

        group.MapPost("/", PlaceOrder);
        group.MapGet("/", async (OrderingDbContext db, CancellationToken ct) =>
            (await db.Orders.AsNoTracking().ToListAsync(ct))
                .OrderByDescending(o => o.CreatedAt).Select(ToDto));
        group.MapGet("/{id:guid}", async (Guid id, OrderingDbContext db, CancellationToken ct) =>
            await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct) is { } o
                ? Results.Ok(ToDto(o))
                : Results.Problem(statusCode: 404, title: "Sipariş bulunamadı"));
        group.MapGet("/{id:guid}/saga", async (Guid id, OrderingDbContext db, CancellationToken ct) =>
            await db.OrderSagas.AsNoTracking().FirstOrDefaultAsync(s => s.CorrelationId == id, ct) is { } s
                ? Results.Ok(new SagaDto(s.CorrelationId, s.CurrentState, s.Total, s.PaymentId, s.FailureReason,
                    s.SubmittedAt, s.UpdatedAt, s.Version))
                : Results.Problem(statusCode: 404, title: "Saga bulunamadı (henüz başlamamış olabilir)"));
        group.MapPost("/{id:guid}/ship", ShipOrder);
        group.MapPost("/{id:guid}/cancel", CancelOrder);
    }

    private static async Task<IResult> PlaceOrder(PlaceOrderRequest request, OrderingDbContext db,
        IPublishEndpoint publishEndpoint, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var items = request.Items ?? [];
        var errors = new Dictionary<string, string[]>();
        if (request.CustomerId == Guid.Empty) errors["customerId"] = ["customerId zorunlu."];
        if (items.Count == 0) errors["items"] = ["Sipariş en az 1 kalem içermeli."];
        if (items.Count > Order.MaxLines) errors["items"] = [$"Sipariş en fazla {Order.MaxLines} kalem içerebilir."];
        if (items.Any(i => i.Quantity <= 0)) errors["items.quantity"] = ["Quantity 0'dan büyük olmalı."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        // Fiyat/isim Ordering'in KENDİ kopyasından okunur → Inventory'ye senkron çağrı yok.
        var ids = items.Select(i => i.ProductId).Distinct().ToList();
        var catalog = await db.CatalogItems.Where(c => ids.Contains(c.ProductId)).ToDictionaryAsync(c => c.ProductId, ct);
        var unknown = ids.Where(id => !catalog.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
            return Results.Problem(statusCode: 400, title: "Bilinmeyen ürün", detail: string.Join(", ", unknown));

        Order order;
        try
        {
            order = Order.Place(request.CustomerId,
                items.Select(i => new OrderLine(i.ProductId, catalog[i.ProductId].Name, i.Quantity, catalog[i.ProductId].Price)));
        }
        catch (DomainException ex)
        {
            return Results.Problem(statusCode: 400, title: "İş kuralı ihlali", detail: ex.Message);
        }

        db.Orders.Add(order);

        // TRANSACTIONAL OUTBOX: UseBusOutbox() sayesinde bu Publish broker'a GİTMEZ;
        // OutboxMessage tablosuna, Order ile AYNI DbContext'e eklenir.
        await publishEndpoint.Publish(new OrderSubmitted(
            order.Id, order.CustomerId, order.Total,
            order.Lines.Select(l => new OrderItem(l.ProductId, l.Quantity, l.UnitPrice)).ToList(),
            order.CreatedAt), ct);

        // Tek SaveChanges = tek SQLite transaction: Order + OutboxMessage birlikte commit edilir (ya hepsi ya hiçbiri).
        // Arka plandaki BusOutboxDeliveryService mesajı RabbitMQ'ya taşır → dual write problemi yok.
        await db.SaveChangesAsync(ct);

        loggerFactory.CreateLogger("Ordering.Orders").LogInformation("Sipariş alındı {OrderId} Müşteri={CustomerId} Tutar={Total}", order.Id, order.CustomerId, order.Total);
        return Results.Created($"/orders/{order.Id}", new { id = order.Id });
    }

    private static async Task<IResult> ShipOrder(Guid id, OrderingDbContext db, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([id], ct);
        if (order is null) return Results.Problem(statusCode: 404, title: "Sipariş bulunamadı");
        try { order.Ship(); }
        catch (DomainException ex) { return Results.Problem(statusCode: 409, title: "Geçersiz durum", detail: ex.Message); }
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CancelOrder(Guid id, OrderingDbContext db, IPublishEndpoint publishEndpoint,
        ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([id], ct);
        if (order is null) return Results.Problem(statusCode: 404, title: "Sipariş bulunamadı");
        try { order.Cancel(); }
        catch (DomainException ex) { return Results.Problem(statusCode: 409, title: "Geçersiz durum", detail: ex.Message); }

        // Yine outbox: durum değişikliği + OrderCancelled event'i atomik. Inventory bunu dinleyip stoğu iade eder.
        await publishEndpoint.Publish(new OrderCancelled(order.Id, "Müşteri iptali"), ct);
        await db.SaveChangesAsync(ct);
        loggerFactory.CreateLogger("Ordering.Orders").LogInformation("Sipariş iptal edildi {OrderId}", order.Id);
        return Results.NoContent();
    }

    private static OrderDto ToDto(Order o) => new(o.Id, o.CustomerId, o.Status.ToString(), o.Total, o.RejectionReason,
        o.CreatedAt, o.Lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice)).ToList());
}
