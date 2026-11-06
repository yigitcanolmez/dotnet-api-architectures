namespace Cqrs.Api.Write.Domain;

// [Write / Domain] Zengin Order aggregate'i: invariant'lar burada korunur ve her durum
// değişikliği bir domain event'i üretir. Ekran ihtiyaçları (ProductNames, ItemCount...) burada YOK —
// onlar okuma modelinin işi. Domain, "nasıl gösterileceğini" bilmez.
public enum OrderStatus { Placed, Shipped, Cancelled }

public sealed class OrderLine
{
    private OrderLine() { } // EF Core için

    public OrderLine(Guid productId, string productName, int quantity, decimal unitPrice)
    {
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
}

public sealed class Order
{
    public const int MaxLines = 10;

    private readonly List<OrderLine> _lines = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    private Order() { } // EF Core için

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines;
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;
    public void ClearDomainEvents() => _domainEvents.Clear();

    public static Order Place(Guid customerId, IReadOnlyList<(Product Product, int Quantity)> items)
    {
        if (items.Count is 0 or > MaxLines)
            throw new DomainException($"Sipariş 1 ile {MaxLines} arasında kalem içermeli.");
        if (items.Any(i => i.Quantity <= 0))
            throw new DomainException("Adet 0'dan büyük olmalı.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Placed,
            CreatedAt = DateTime.UtcNow
        };
        foreach (var (product, quantity) in items)
            order._lines.Add(new OrderLine(product.Id, product.Name, quantity, product.Price));
        order.Total = order._lines.Sum(l => l.UnitPrice * l.Quantity);

        order._domainEvents.Add(new OrderPlaced(
            order.Id, customerId, order.Total, order._lines.Sum(l => l.Quantity),
            order._lines.Select(l => l.ProductName).Distinct().ToList(), order.CreatedAt));
        return order;
    }

    public void Ship()
    {
        if (Status != OrderStatus.Placed)
            throw new DomainException($"Sadece 'Placed' durumundaki sipariş kargolanabilir. Mevcut durum: {Status}.");

        Status = OrderStatus.Shipped;
        _domainEvents.Add(new OrderShipped(Id, CustomerId, DateTime.UtcNow));
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Shipped)
            throw new DomainException("Kargolanmış sipariş iptal edilemez.");
        if (Status == OrderStatus.Cancelled)
            throw new DomainException("Sipariş zaten iptal edilmiş.");

        Status = OrderStatus.Cancelled;
        _domainEvents.Add(new OrderCancelled(Id, CustomerId, Total, DateTime.UtcNow));
    }
}
