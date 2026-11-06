namespace Layered.DataAccess.Entities;

// [Data Access Katmanı] Order tablosunun birebir yansıması (database-driven design).
// - public set: Status = Shipped iken bile herkes Status = Cancelled yazabilir; engelleyen yok.
// - Lines bir EF navigation property'si; şekli EF'in ihtiyaçlarına göre belirlendi.
public class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public OrderStatus Status { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
}
