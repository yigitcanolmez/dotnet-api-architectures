// [Ordering — Domain] Sipariş aggregate'i. Sadece Ordering servisinin kendi verisi;
// stok veya ödeme bilgisi BURADA YOK (database-per-service). Diğer servislerle sadece mesajla konuşulur.
namespace Ordering.Api.Domain;

public enum OrderStatus
{
    Pending,    // Saga sürüyor (semantic lock: "işlemde" bayrağı)
    Confirmed,  // Stok ayrıldı + ödeme alındı
    Rejected,   // Saga başarısız (stok yok / ödeme reddedildi)
    Shipped,
    Cancelled
}

public class DomainException(string message) : Exception(message);

public class OrderLine
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    private OrderLine() { }

    public OrderLine(Guid productId, string productName, int quantity, decimal unitPrice)
    {
        if (quantity <= 0) throw new DomainException("Quantity 0'dan büyük olmalı.");
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}

public class Order
{
    public const int MaxLines = 10;

    private readonly List<OrderLine> _lines = [];

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines;
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Order() { }

    public static Order Place(Guid customerId, IEnumerable<OrderLine> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0) throw new DomainException("Sipariş en az 1 kalem içermeli.");
        if (list.Count > MaxLines) throw new DomainException($"Sipariş en fazla {MaxLines} kalem içerebilir.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        order._lines.AddRange(list);
        order.Total = list.Sum(l => l.UnitPrice * l.Quantity);
        return order;
    }

    // Confirm/Reject idempotent: aynı event iki kez gelirse (at-least-once) sorun çıkmaz.
    public void Confirm()
    {
        if (Status == OrderStatus.Confirmed) return;
        if (Status != OrderStatus.Pending) throw new DomainException($"{Status} durumundaki sipariş onaylanamaz.");
        Status = OrderStatus.Confirmed;
    }

    public void Reject(string reason)
    {
        if (Status == OrderStatus.Rejected) return;
        if (Status != OrderStatus.Pending) throw new DomainException($"{Status} durumundaki sipariş reddedilemez.");
        Status = OrderStatus.Rejected;
        RejectionReason = reason;
    }

    public void Ship()
    {
        if (Status != OrderStatus.Confirmed)
            throw new DomainException($"Sadece onaylanmış (Confirmed) sipariş kargolanabilir. Mevcut durum: {Status}.");
        Status = OrderStatus.Shipped;
    }

    public void Cancel()
    {
        switch (Status)
        {
            case OrderStatus.Shipped: throw new DomainException("Kargolanmış sipariş iptal edilemez.");
            case OrderStatus.Cancelled: throw new DomainException("Sipariş zaten iptal edilmiş.");
            case OrderStatus.Rejected: throw new DomainException("Reddedilmiş sipariş iptal edilemez.");
            case OrderStatus.Pending: throw new DomainException("Sipariş saga'sı hâlâ sürüyor (Pending); sonuçlanınca tekrar deneyin.");
        }
        Status = OrderStatus.Cancelled;
    }
}
