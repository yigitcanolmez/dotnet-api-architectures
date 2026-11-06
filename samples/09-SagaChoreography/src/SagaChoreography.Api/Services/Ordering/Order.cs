namespace SagaChoreography.Services.Ordering;

// [Ordering Servisi — Domain] Saga'nın "sahibi" olan aggregate. Durum makinesi entity içinde:
//   Pending ──PaymentCompleted──► Confirmed ──ship──► Shipped
//      └──StockReservationFailed / PaymentFailed──► Rejected
//   Confirmed ──cancel──► Cancelled (OrderCancelled → stok + ödeme iadesi)
public sealed class Order
{
    public const int MaxLines = 10;
    private readonly List<OrderLine> _lines = [];

    private Order() { }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? StatusReason { get; private set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines;
    public decimal Total => _lines.Sum(l => l.Quantity * l.UnitPrice);

    public static Order Create(Guid customerId, IReadOnlyCollection<OrderLine> lines)
    {
        if (customerId == Guid.Empty) throw new OrderRuleException("CustomerId zorunludur.");
        if (lines.Count is 0 or > MaxLines) throw new OrderRuleException($"Sipariş 1 ile {MaxLines} arasında kalem içermelidir.");
        if (lines.Any(l => l.Quantity <= 0)) throw new OrderRuleException("Miktar 0'dan büyük olmalıdır.");

        var order = new Order { Id = Guid.NewGuid(), CustomerId = customerId, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow };
        order._lines.AddRange(lines);
        return order;
    }

    // Doğal idempotency: aynı olay ikinci kez gelirse durum zaten değişmiştir, hiçbir şey yapılmaz.
    public void Confirm()
    {
        if (Status == OrderStatus.Pending) Status = OrderStatus.Confirmed;
    }

    public void Reject(string reason)
    {
        if (Status != OrderStatus.Pending) return;
        Status = OrderStatus.Rejected;
        StatusReason = reason;
    }

    public void Ship()
    {
        if (Status != OrderStatus.Confirmed)
            throw new OrderConflictException($"Sadece 'Confirmed' sipariş kargolanabilir (mevcut: {Status}).");
        Status = OrderStatus.Shipped;
    }

    public void Cancel(string reason)
    {
        switch (Status)
        {
            case OrderStatus.Shipped: throw new OrderConflictException("Kargolanmış sipariş iptal edilemez.");
            case OrderStatus.Cancelled: throw new OrderConflictException("Sipariş zaten iptal edilmiş.");
            case OrderStatus.Rejected: throw new OrderConflictException("Reddedilmiş sipariş iptal edilemez.");
            case OrderStatus.Pending: throw new OrderConflictException("Saga henüz tamamlanmadı (Pending); biraz sonra tekrar deneyin.");
        }
        Status = OrderStatus.Cancelled;
        StatusReason = reason;
    }
}

public sealed record OrderLine(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);

public enum OrderStatus { Pending, Confirmed, Rejected, Shipped, Cancelled }

public sealed class OrderRuleException(string message) : Exception(message);
public sealed class OrderConflictException(string message) : Exception(message);

// Ordering'in fiyatları bilmek için tuttuğu KENDİ ürün kopyası (database-per-service: Inventory'nin tablosuna erişemez).
public sealed class CatalogItem(Guid id, string name, decimal price)
{
    public Guid Id { get; private set; } = id;
    public string Name { get; private set; } = name;
    public decimal Price { get; private set; } = price;
}
