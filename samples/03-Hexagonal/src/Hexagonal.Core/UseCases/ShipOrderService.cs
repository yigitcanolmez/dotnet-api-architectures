using Hexagonal.Core.Exceptions;
using Hexagonal.Core.Ports.In;
using Hexagonal.Core.Ports.Out;

namespace Hexagonal.Core.UseCases;

public class ShipOrderService(IOrderRepository orders, INotifier notifier) : IShipOrderUseCase
{
    public async Task ExecuteAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await orders.GetAsync(orderId, ct) ?? throw new OrderNotFoundException(orderId);
        order.Ship();
        await orders.SaveChangesAsync(ct);
        await notifier.NotifyAsync(orderId, "Sipariş kargolandı.", ct);
    }
}
