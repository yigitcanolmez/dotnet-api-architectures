using VerticalSlice.Api.Common;

namespace VerticalSlice.Api.Domain;

// [Domain] VSA + DDD hibrit: okuma dilimleri bu sınıfı hiç kullanmaz (doğrudan projeksiyon yapar),
// ama durum değiştiren dilimler (PlaceOrder/Ship/Cancel) invariant'ları burada korur.
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

    private Order() { } // EF Core için

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines;
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Result<Order> Place(Guid customerId, IReadOnlyList<(Product Product, int Quantity)> items)
    {
        if (items.Count is 0 or > MaxLines)
            return Error.Validation($"Sipariş 1 ile {MaxLines} arasında kalem içermeli.");
        if (items.Any(i => i.Quantity <= 0))
            return Error.Validation("Adet 0'dan büyük olmalı.");

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
        return order;
    }

    public Result Ship()
    {
        if (Status != OrderStatus.Placed)
            return Error.Conflict($"Sadece 'Placed' durumundaki sipariş kargolanabilir. Mevcut durum: {Status}.");

        Status = OrderStatus.Shipped;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status == OrderStatus.Shipped)
            return Error.Conflict("Kargolanmış sipariş iptal edilemez.");
        if (Status == OrderStatus.Cancelled)
            return Error.Conflict("Sipariş zaten iptal edilmiş.");

        Status = OrderStatus.Cancelled;
        return Result.Success();
    }
}
