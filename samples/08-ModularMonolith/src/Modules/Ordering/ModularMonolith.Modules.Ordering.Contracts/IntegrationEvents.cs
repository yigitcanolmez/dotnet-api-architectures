using ModularMonolith.Shared.Abstractions.Events;

namespace ModularMonolith.Modules.Ordering.Contracts;

// [Ordering.Contracts — Integration Events] Ordering, dinleyicilerini (Catalog, Shipping) TANIMAZ;
// sadece "ne olduğunu" duyurur. Event-Carried State Transfer: tüketicinin ihtiyaç duyduğu veri event'te.

public sealed record OrderPlacedIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    IReadOnlyList<OrderPlacedLine> Lines,
    decimal Total) : IntegrationEvent;

public sealed record OrderPlacedLine(Guid ProductId, string ProductName, int Quantity);

public sealed record OrderShippedIntegrationEvent(Guid OrderId) : IntegrationEvent;

public sealed record OrderCancelledIntegrationEvent(Guid OrderId, string Reason) : IntegrationEvent;
