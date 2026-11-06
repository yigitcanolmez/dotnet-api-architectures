using MediatR;

namespace Clean.Application.Orders.Commands.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId, string Reason) : IRequest;
