using Onion.Application.Dtos;
using Onion.Application.Exceptions;
using Onion.Application.Interfaces;
using Onion.Domain.Entities;
using Onion.Domain.Interfaces;
using Onion.Domain.Services;

namespace Onion.Application.Orders;

// [Application Services halkası] Use case ORKESTRASYONU: yükle → domain'e kuralı uygulat → kaydet → bildir.
// İş kuralı YOK (karşılaştırın: 01-Layered/OrderService). Sadece iç halkalardaki arayüzleri kullanır.
public class OrderAppService(
    IOrderRepository orders,
    OrderPlacementService placement,
    INotificationService notifications)
{
    public async Task<Guid> PlaceAsync(PlaceOrderRequest request, CancellationToken ct = default)
    {
        var items = (request.Items ?? []).Select(i => (i.ProductId, i.Quantity)).ToList();

        var order = await placement.PlaceAsync(request.CustomerId, items, DateTime.UtcNow, ct);

        await orders.AddAsync(order, ct);
        await orders.SaveChangesAsync(ct);
        await notifications.NotifyOrderPlacedAsync(order.Id, order.Total, ct);
        return order.Id;
    }

    public async Task<OrderDto> GetAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await LoadAsync(id, ct));

    public async Task<IReadOnlyList<OrderDto>> ListAsync(CancellationToken ct = default) =>
        (await orders.ListAsync(ct)).Select(ToDto).ToList();

    public async Task ShipAsync(Guid id, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, ct);
        order.Ship();                         // kural domain'de
        await orders.SaveChangesAsync(ct);
        await notifications.NotifyOrderShippedAsync(id, ct);
    }

    public async Task CancelAsync(Guid id, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, ct);
        order.Cancel();                       // kural domain'de
        await orders.SaveChangesAsync(ct);
        await notifications.NotifyOrderCancelledAsync(id, ct);
    }

    private async Task<Order> LoadAsync(Guid id, CancellationToken ct) =>
        await orders.GetAsync(id, ct) ?? throw new NotFoundException($"Sipariş bulunamadı: {id}");

    private static OrderDto ToDto(Order o) => new(
        o.Id, o.CustomerId, o.Status.ToString(), o.Total, o.CreatedAt,
        o.Lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice, l.LineTotal)).ToList());
}
