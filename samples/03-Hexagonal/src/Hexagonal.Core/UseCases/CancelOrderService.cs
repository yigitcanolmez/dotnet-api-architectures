using Hexagonal.Core.Exceptions;
using Hexagonal.Core.Ports.In;
using Hexagonal.Core.Ports.Out;

namespace Hexagonal.Core.UseCases;

public class CancelOrderService(IOrderRepository orders, INotifier notifier) : ICancelOrderUseCase
{
    public async Task ExecuteAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await orders.GetAsync(orderId, ct) ?? throw new OrderNotFoundException(orderId);
        order.Cancel(); // kural domain'de
        await orders.SaveChangesAsync(ct);
        await notifier.NotifyAsync(orderId, "Sipariş iptal edildi.", ct);
    }
}
