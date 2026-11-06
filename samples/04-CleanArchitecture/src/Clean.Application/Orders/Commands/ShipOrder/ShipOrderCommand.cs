using MediatR;

namespace Clean.Application.Orders.Commands.ShipOrder;

public sealed record ShipOrderCommand(Guid OrderId, string TrackingNumber) : IRequest;
