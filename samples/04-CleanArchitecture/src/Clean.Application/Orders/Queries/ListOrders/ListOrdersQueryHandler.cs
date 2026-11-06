using Clean.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Clean.Application.Orders.Queries.ListOrders;

public sealed class ListOrdersQueryHandler(IApplicationDbContext db) : IRequestHandler<ListOrdersQuery, IReadOnlyList<OrderDto>>
{
    public async Task<IReadOnlyList<OrderDto>> Handle(ListOrdersQuery request, CancellationToken cancellationToken) =>
        await db.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(OrderProjections.ToDto)
            .ToListAsync(cancellationToken);
}
