namespace SagaChoreography.Services.Inventory;

// [Inventory Servisi — Domain] Stok ve rezervasyon. Rezervasyon "compensatable" bir adımdır:
// Release() ile semantik olarak geri alınabilir (silme değil, iade).
public sealed class Stock(Guid productId, string productName, int available)
{
    public Guid ProductId { get; private set; } = productId;
    public string ProductName { get; private set; } = productName;
    public int Available { get; private set; } = available;

    // Optimistic concurrency: aynı anda iki sipariş aynı stoğu düşmeye çalışırsa biri DbUpdateConcurrencyException
    // alır ve MassTransit retry ile mesajı yeniden işler.
    public int Version { get; private set; }

    public void Reserve(int quantity)
    {
        if (quantity <= 0 || Available < quantity)
            throw new InvalidOperationException($"'{ProductName}' için yeterli stok yok.");
        Available -= quantity;
        Version++;
    }

    public void Release(int quantity)
    {
        Available += quantity;
        Version++;
    }
}

public sealed class Reservation
{
    private readonly List<ReservationLine> _lines = [];

    private Reservation() { }

    public Reservation(Guid orderId, IEnumerable<ReservationLine> lines)
    {
        OrderId = orderId;
        _lines.AddRange(lines);
        Status = ReservationStatus.Reserved;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid OrderId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyCollection<ReservationLine> Lines => _lines;

    public void MarkReleased() => Status = ReservationStatus.Released;
}

public sealed record ReservationLine(Guid ProductId, int Quantity);

public enum ReservationStatus { Reserved, Released }
