using MediatR;

namespace Clean.Application.Orders.Queries.ListOrders;

public sealed record ListOrdersQuery : IRequest<IReadOnlyList<OrderDto>>;
