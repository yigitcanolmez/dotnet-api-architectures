using Cqrs.Api.Common;

namespace Cqrs.Api.Write.Commands;

public sealed record CancelOrderCommand(Guid OrderId);

public sealed class CancelOrderHandler(WriteDbContext db) : ICommandHandler<CancelOrderCommand>
{
    public async Task HandleAsync(CancelOrderCommand command, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([command.OrderId], ct)
                    ?? throw new NotFoundException($"Sipariş bulunamadı: {command.OrderId}");
        order.Cancel(); // kural ihlali → DomainException → 409
        await db.SaveChangesAsync(ct); // → OrderCancelled
    }
}
