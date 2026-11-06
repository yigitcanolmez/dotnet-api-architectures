using Clean.Application.Abstractions;
using Clean.Application.Common.Exceptions;
using Clean.Domain.Orders;
using MediatR;

namespace Clean.Application.Orders.Commands.ShipOrder;

public sealed class ShipOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<ShipOrderCommand>
{
    public async Task Handle(ShipOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.Orders.FindAsync([new OrderId(request.OrderId)], cancellationToken)
                    ?? throw new NotFoundException(nameof(Order), request.OrderId);

        order.Ship(request.TrackingNumber); // kural: sadece Placed kargolanabilir (Domain'de)

        await db.SaveChangesAsync(cancellationToken);
    }
}
