using Onion.Domain.Exceptions;

namespace Onion.Domain.Entities;

// [Domain Model — soğanın merkezi] RICH model: veri + davranış bir arada.
// - Tüm setter'lar private → durum sadece Place/Ship/Cancel ile değişir, kurallar atlanamaz.
// - Kalemler private List'te (backing field), dışarıya IReadOnlyList olarak açılır.
// - EF Core, ASP.NET veya başka bir framework'e referans YOK.
public class Order
{
    public const int MaxLines = 10;

    private readonly List<OrderLine> _lines = new();

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    public decimal Total => _lines.Sum(l => l.LineTotal);

    private Order() { } // EF Core için

    public static Order Place(Guid customerId, IReadOnlyCollection<(Product Product, int Quantity)> items, DateTime createdAt)
    {
        // Kural 1: en az 1, en fazla 10 kalem
        if (items.Count == 0) throw new DomainException("Sipariş en az 1 kalem içermelidir.");
        if (items.Count > MaxLines) throw new DomainException($"Sipariş en fazla {MaxLines} kalem içerebilir.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Placed,
            CreatedAt = createdAt,
        };

        foreach (var (product, quantity) in items)
            order._lines.Add(new OrderLine(product.Id, product.Name, quantity, product.Price));

        return order;
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
