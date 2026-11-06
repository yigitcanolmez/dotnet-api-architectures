using Layered.Business.Dtos;
using Layered.Business.Exceptions;
using Layered.DataAccess.Entities;
using Layered.DataAccess.Repositories;

namespace Layered.Business.Services;

// [Business Katmanı] Transaction Script + Anemic Domain Model.
// TÜM iş kuralları burada; entity'ler sadece veri taşıyor.
//
// Sorunlar (bilinçli olarak gösteriliyor):
//  1) Constructor'daki tipler DataAccess'in SOMUT sınıfları → bu servis veritabanı olmadan oluşturulamaz.
//  2) Kurallar (ör. "kargolanan iptal edilemez") Order'ın kendisinde değil; başka bir servis
//     order.Status = Cancelled yazarak kuralı kolayca atlayabilir.
//  3) Yeni özellik geldikçe bu sınıf büyür → "God Service".
public class OrderService(OrderRepository orders, ProductRepository products)
{
    private const int MaxLines = 10;

    public async Task<Guid> PlaceAsync(PlaceOrderRequest request, CancellationToken ct = default)
    {
        var items = request.Items ?? [];

        // Kural 1: en az 1, en fazla 10 kalem
        if (items.Count == 0)
            throw new BusinessException("Sipariş en az 1 kalem içermelidir.");
        if (items.Count > MaxLines)
            throw new BusinessException($"Sipariş en fazla {MaxLines} kalem içerebilir.");

        // Kural 2: Quantity > 0
        if (items.Any(i => i.Quantity <= 0))
            throw new BusinessException("Miktar 0'dan büyük olmalıdır.");

        // Kural 5: bilinmeyen ürün
        var found = await products.GetByIdsAsync(items.Select(i => i.ProductId), ct);
        var unknown = items.Select(i => i.ProductId).Except(found.Select(p => p.Id)).ToList();
        if (unknown.Count > 0)
            throw new BusinessException($"Bilinmeyen ürün: {string.Join(", ", unknown)}");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Status = OrderStatus.Placed,
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var item in items)
        {
            var product = found.First(p => p.Id == item.ProductId);
            order.Lines.Add(new OrderLine
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
            });
        }

        // Toplamı hesaplamak da servisin işi; unutan bir başka servis yanlış Total yazabilir.
        order.Total = order.Lines.Sum(l => l.UnitPrice * l.Quantity);

        orders.Add(order);
        await orders.SaveChangesAsync(ct);
        return order.Id;
    }

    public async Task<OrderDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var order = await orders.GetAsync(id, ct) ?? throw new NotFoundException($"Sipariş bulunamadı: {id}");
        return ToDto(order);
    }

    public async Task<IReadOnlyList<OrderDto>> ListAsync(CancellationToken ct = default) =>
        (await orders.ListAsync(ct)).Select(ToDto).ToList();

    public async Task ShipAsync(Guid id, CancellationToken ct = default)
    {
        var order = await orders.GetAsync(id, ct) ?? throw new NotFoundException($"Sipariş bulunamadı: {id}");

        // Kural 3: sadece Placed sipariş kargolanabilir
        if (order.Status != OrderStatus.Placed)
            throw new BusinessException($"Sadece 'Placed' durumundaki sipariş kargolanabilir. Mevcut durum: {order.Status}");

        order.Status = OrderStatus.Shipped;
        await orders.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Guid id, CancellationToken ct = default)
    {
        var order = await orders.GetAsync(id, ct) ?? throw new NotFoundException($"Sipariş bulunamadı: {id}");

        // Kural 4: Shipped iptal edilemez, Cancelled tekrar iptal edilemez
        if (order.Status == OrderStatus.Shipped)
            throw new BusinessException("Kargolanan sipariş iptal edilemez.");
        if (order.Status == OrderStatus.Cancelled)
            throw new BusinessException("Sipariş zaten iptal edilmiş.");

        order.Status = OrderStatus.Cancelled;
        await orders.SaveChangesAsync(ct);
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id, o.CustomerId, o.Status.ToString(), o.Total, o.CreatedAt,
        o.Lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice, l.UnitPrice * l.Quantity)).ToList());
}
