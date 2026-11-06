namespace Hexagonal.Core.Domain;

// [Core / Domain] Sipariş aggregate'i. Port'ları ve adapter'ları bilmez; sadece kurallar.
// (Hexagonal, içerinin nasıl organize edileceğini dikte etmez — burada basit bir rich model seçtik.)
public class Order
{
    public const int MaxLines = 10;

    private readonly List<OrderLine> _lines = new();

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? PaymentTransactionId { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    public decimal Total => _lines.Sum(l => l.LineTotal);

    private Order() { }

    public static Order Place(
        Guid customerId,
        IReadOnlyCollection<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)> lines,
        DateTime createdAt)
    {
        // Kural 1: en az 1, en fazla 10 kalem
        if (lines.Count == 0) throw new DomainException("Sipariş en az 1 kalem içermelidir.");
        if (lines.Count > MaxLines) throw new DomainException($"Sipariş en fazla {MaxLines} kalem içerebilir.");

        var order = new Order { Id = Guid.NewGuid(), CustomerId = customerId, Status = OrderStatus.Placed, CreatedAt = createdAt };
        foreach (var l in lines)
            order._lines.Add(new OrderLine(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice));
        return order;
    }

    public void MarkPaid(string transactionId)
    {
        if (PaymentTransactionId is not null) throw new DomainException("Sipariş zaten ödenmiş.");
        PaymentTransactionId = transactionId;
    }

    // Kural 3: sadece Placed sipariş kargolanabilir
    public void Ship()
    {
        if (Status != OrderStatus.Placed)
            throw new DomainException($"Sadece 'Placed' durumundaki sipariş kargolanabilir. Mevcut durum: {Status}");
        Status = OrderStatus.Shipped;
    }

    // Kural 4: Shipped iptal edilemez, Cancelled tekrar iptal edilemez
    public void Cancel()
    {
        if (Status == OrderStatus.Shipped) throw new DomainException("Kargolanan sipariş iptal edilemez.");
        if (Status == OrderStatus.Cancelled) throw new DomainException("Sipariş zaten iptal edilmiş.");
        Status = OrderStatus.Cancelled;
    }
}
