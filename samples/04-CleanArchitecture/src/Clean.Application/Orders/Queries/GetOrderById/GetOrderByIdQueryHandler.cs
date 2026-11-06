using Clean.Application.Abstractions;
using Clean.Application.Common.Exceptions;
using Clean.Domain.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Clean.Application.Orders.Queries.GetOrderById;

public sealed class GetOrderByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var orderId = new OrderId(request.OrderId);

        return await db.Orders
                   .AsNoTracking()
                   .Where(o => o.Id == orderId)
                   .Select(OrderProjections.ToDto)
                   .SingleOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException(nameof(Order), request.OrderId);
    }
}
