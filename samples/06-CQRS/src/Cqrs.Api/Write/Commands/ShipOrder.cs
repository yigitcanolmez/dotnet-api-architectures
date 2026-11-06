using Cqrs.Api.Common;

namespace Cqrs.Api.Write.Commands;

public sealed record ShipOrderCommand(Guid OrderId);

// void komut: yükle → domain kararı → kaydet. Sonuç okuma modeline event ile (gecikmeli) yansır.
public sealed class ShipOrderHandler(WriteDbContext db) : ICommandHandler<ShipOrderCommand>
{
    public async Task HandleAsync(ShipOrderCommand command, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([command.OrderId], ct)
                    ?? throw new NotFoundException($"Sipariş bulunamadı: {command.OrderId}");
        order.Ship();
        await db.SaveChangesAsync(ct); // → OrderShipped
    }
}
