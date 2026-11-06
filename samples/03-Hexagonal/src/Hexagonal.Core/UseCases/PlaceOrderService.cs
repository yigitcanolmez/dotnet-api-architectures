using Hexagonal.Core.Domain;
using Hexagonal.Core.Exceptions;
using Hexagonal.Core.Ports.In;
using Hexagonal.Core.Ports.Out;

namespace Hexagonal.Core.UseCases;

// [Core / Use Case] Inbound port'u implemente eder, sadece outbound port'ları kullanır.
// Kim çağırıyor (HTTP mi, kuyruk mu, test mi) bilmez; kimi çağırdığını (EF mi, Stripe mı) da bilmez.
public class PlaceOrderService(
    IProductCatalog catalog,
    IPaymentGateway payments,
    IOrderRepository orders,
    INotifier notifier) : IPlaceOrderUseCase
{
    public async Task<Guid> ExecuteAsync(PlaceOrderCommand command, CancellationToken ct = default)
    {
        var items = command.Items ?? [];

        // Kural 5: bilinmeyen ürün → hata
        var products = await catalog.GetByIdsAsync(items.Select(i => i.ProductId), ct);
        var unknown = items.Select(i => i.ProductId).Except(products.Select(p => p.Id)).ToList();
        if (unknown.Count > 0)
            throw new DomainException($"Bilinmeyen ürün: {string.Join(", ", unknown)}");

        var lines = items.Select(i =>
        {
            var p = products.First(x => x.Id == i.ProductId);
            return (p.Id, p.Name, i.Quantity, p.Price);
        }).ToList();

        var order = Order.Place(command.CustomerId, lines, DateTime.UtcNow);

        var payment = await payments.ChargeAsync(command.CustomerId, order.Total, ct);
        if (!payment.Success)
            throw new PaymentDeclinedException(payment.Reason ?? "bilinmeyen sebep");
        order.MarkPaid(payment.TransactionId!);

        await orders.AddAsync(order, ct);
        await orders.SaveChangesAsync(ct);
        await notifier.NotifyAsync(order.Id, $"Sipariş alındı, ödeme {payment.TransactionId} ile çekildi. Tutar: {order.Total} TRY", ct);
        return order.Id;
    }
}
