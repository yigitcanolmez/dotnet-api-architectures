using ModularMonolith.Shared.Abstractions.Results;

namespace ModularMonolith.Modules.Ordering.Domain;

// [Ordering — Zengin Domain Modeli] İş kuralları (invariant'lar) entity'nin içinde korunur.
// internal: Ordering dışındaki hiçbir modül Order'ı göremez; onlar sadece integration event'leri görür.
internal sealed class Order
{
    public const int MaxLines = 10;

    private readonly List<OrderLine> _lines = [];

    private Order() { }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines;
    public decimal Total => _lines.Sum(l => l.LineTotal);

    public static Result<Order> Place(Guid customerId, IReadOnlyCollection<OrderLine> lines, DateTime now)
    {
        if (customerId == Guid.Empty)
            return Error.Validation("Order.CustomerRequired", "CustomerId zorunludur.");
        if (lines.Count is 0 or > MaxLines)
            return Error.Validation("Order.LineCount", $"Sipariş 1 ile {MaxLines} arasında kalem içermelidir.");
        if (lines.Any(l => l.Quantity <= 0))
            return Error.Validation("Order.Quantity", "Miktar 0'dan büyük olmalıdır.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Placed,
            CreatedAt = now
        };
        order._lines.AddRange(lines);
        return order;
    }

    public Result Ship()
    {
        if (Status != OrderStatus.Placed)
            return Error.Conflict("Order.CannotShip", $"Sadece 'Placed' sipariş kargolanabilir (mevcut: {Status}).");
        Status = OrderStatus.Shipped;
        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status == OrderStatus.Shipped)
            return Error.Conflict("Order.AlreadyShipped", "Kargolanmış sipariş iptal edilemez.");
        if (Status == OrderStatus.Cancelled)
            return Error.Conflict("Order.AlreadyCancelled", "Sipariş zaten iptal edilmiş.");
        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        return Result.Success();
    }
}

internal sealed record OrderLine(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

internal enum OrderStatus { Placed, Shipped, Cancelled }
