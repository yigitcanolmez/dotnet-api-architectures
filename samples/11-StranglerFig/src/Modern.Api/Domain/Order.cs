namespace Modern.Api.Domain;

public enum OrderStatus { Placed, Shipped, Cancelled }

// [Domain] Rich domain model: legacy'de controller'a dağılmış iş kuralları burada, tek yerde.
public sealed class Order
{
    private readonly List<OrderLine> _lines = [];

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public IReadOnlyList<OrderLine> Lines => _lines;

    public Money Total => _lines.Aggregate(Money.Zero(Currency), (sum, l) => sum + l.LineTotal);

    private Order() { } // EF Core

    public static Order Place(Guid customerId, IReadOnlyCollection<(Product Product, int Quantity)> items)
    {
        if (items.Count is < 1 or > 10) throw new DomainException("Sipariş 1 ile 10 arasında kalem içermelidir.");

        var order = new Order { Id = Guid.NewGuid(), CustomerId = customerId, Status = OrderStatus.Placed, CreatedAt = DateTime.UtcNow };
        foreach (var (product, quantity) in items)
        {
            if (quantity <= 0) throw new DomainException("Adet 0'dan büyük olmalıdır.");
            if (!product.IsSellable) throw new DomainException($"'{product.Name}' satışta değil.");
            order._lines.Add(new OrderLine(product.Id, product.Name, quantity, product.Price.Amount, product.Price.Currency));
        }
        order.Currency = order._lines[0].Currency;
        _ = order.Total; // para birimi tutarlılığını doğrular
        return order;
    }

    public void Ship()
    {
        if (Status != OrderStatus.Placed) throw new DomainException("Sadece 'Placed' durumundaki sipariş kargolanabilir.", isConflict: true);
        Status = OrderStatus.Shipped;
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Shipped) throw new DomainException("Kargolanmış sipariş iptal edilemez.", isConflict: true);
        if (Status == OrderStatus.Cancelled) throw new DomainException("Sipariş zaten iptal edilmiş.", isConflict: true);
        Status = OrderStatus.Cancelled;
    }
}

public sealed class OrderLine
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = "";
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; } = "TRY";

    public Money LineTotal => new Money(UnitPrice, Currency) * Quantity;

    private OrderLine() { } // EF Core

    public OrderLine(Guid productId, string productName, int quantity, decimal unitPrice, string currency)
        => (ProductId, ProductName, Quantity, UnitPrice, Currency) = (productId, productName, quantity, unitPrice, currency);
}
