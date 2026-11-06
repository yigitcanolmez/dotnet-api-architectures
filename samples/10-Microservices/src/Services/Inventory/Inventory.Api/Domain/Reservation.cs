// [Inventory — Domain] Bir siparişin stok rezervasyonu. OrderId UNIQUE → doğal idempotency.
namespace Inventory.Api.Domain;

public enum ReservationStatus { Reserved, Released, Rejected }

public class ReservationItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

public class Reservation
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
    public List<ReservationItem> Items { get; private set; } = [];

    private Reservation() { }

    public static Reservation Reserved(Guid orderId, IEnumerable<ReservationItem> items) => new()
    {
        Id = Guid.NewGuid(), OrderId = orderId, Status = ReservationStatus.Reserved,
        CreatedAt = DateTime.UtcNow, Items = items.ToList()
    };

    // Reddedilen talebi de kaydediyoruz: hem denetim izi hem de tekrar gelen ReserveStock için aynı cevabı vermek.
    public static Reservation Rejected(Guid orderId, IEnumerable<ReservationItem> items, string reason) => new()
    {
        Id = Guid.NewGuid(), OrderId = orderId, Status = ReservationStatus.Rejected, Reason = reason,
        CreatedAt = DateTime.UtcNow, Items = items.ToList()
    };

    /// <summary>Compensation. Sadece Reserved ise stok iade edilir; ikinci kez çağrılırsa hiçbir şey yapmaz.</summary>
    public bool Release(string reason, IReadOnlyDictionary<Guid, Product> products)
    {
        if (Status != ReservationStatus.Reserved) return false;
        foreach (var item in Items) products[item.ProductId].Release(item.Quantity);
        Status = ReservationStatus.Released;
        Reason = reason;
        ReleasedAt = DateTime.UtcNow;
        return true;
    }
}
