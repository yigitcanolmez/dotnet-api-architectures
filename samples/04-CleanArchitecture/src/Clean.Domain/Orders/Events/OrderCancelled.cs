using Clean.Domain.Common;

namespace Clean.Domain.Orders.Events;

public sealed record OrderCancelled(
    OrderId OrderId,
    string Reason,
    IReadOnlyList<OrderPlacedLine> Lines) : IDomainEvent;
