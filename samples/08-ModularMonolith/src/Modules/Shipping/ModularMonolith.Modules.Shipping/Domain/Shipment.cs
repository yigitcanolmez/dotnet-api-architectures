namespace ModularMonolith.Modules.Shipping.Domain;

// [Shipping — iç model] Sipariş değil, "kargo" kavramı. Ordering'in Order sınıfını bilmez;
// ihtiyaç duyduğu veriyi OrderPlaced event'inden kendi modeline kopyalar.
internal sealed class Shipment
{
    private Shipment() { }

    public Shipment(Guid orderId, int itemCount, DateTime now)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        ItemCount = itemCount;
        Status = ShipmentStatus.AwaitingShipment;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public int ItemCount { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Doğal idempotency: aynı duruma tekrar geçmek zararsızdır.
    public void MarkShipped()
    {
        if (Status == ShipmentStatus.AwaitingShipment)
            Status = ShipmentStatus.Shipped;
    }

    public void Cancel()
    {
        if (Status == ShipmentStatus.AwaitingShipment)
            Status = ShipmentStatus.Cancelled;
    }
}

internal enum ShipmentStatus { AwaitingShipment, Shipped, Cancelled }
