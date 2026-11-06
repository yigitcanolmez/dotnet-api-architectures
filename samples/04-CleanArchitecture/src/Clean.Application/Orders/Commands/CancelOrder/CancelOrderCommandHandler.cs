using Clean.Application.Abstractions;
using Clean.Application.Common.Exceptions;
using Clean.Domain.Orders;
using MediatR;

namespace Clean.Application.Orders.Commands.CancelOrder;

// Use case: siparişi iptal et. "Kargolanmış sipariş iptal edilemez" kuralını handler BİLMEZ;
// Order.Cancel ihlalde DomainException fırlatır → GlobalExceptionHandler → 409.
public sealed class CancelOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelOrderCommand>
{
    public async Task Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.Orders.FindAsync([new OrderId(request.OrderId)], cancellationToken)
                    ?? throw new NotFoundException(nameof(Order), request.OrderId);

        order.Cancel(request.Reason);

        await db.SaveChangesAsync(cancellationToken); // → OrderCancelled → OrderCancelledHandler (stok iadesi)
    }
}
