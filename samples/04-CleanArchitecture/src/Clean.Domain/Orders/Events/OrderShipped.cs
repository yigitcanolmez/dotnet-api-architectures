using Clean.Domain.Common;

namespace Clean.Domain.Orders.Events;

public sealed record OrderShipped(OrderId OrderId, string TrackingNumber) : IDomainEvent;
