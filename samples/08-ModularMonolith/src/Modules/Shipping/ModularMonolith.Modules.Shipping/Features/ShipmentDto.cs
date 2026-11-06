using ModularMonolith.Modules.Shipping.Domain;

namespace ModularMonolith.Modules.Shipping.Features;

internal sealed record ShipmentDto(Guid Id, Guid OrderId, int ItemCount, string Status, DateTime CreatedAt)
{
    public static ShipmentDto From(Shipment s) => new(s.Id, s.OrderId, s.ItemCount, s.Status.ToString(), s.CreatedAt);
}
