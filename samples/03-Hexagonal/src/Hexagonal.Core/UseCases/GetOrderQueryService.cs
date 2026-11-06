using Hexagonal.Core.Domain;
using Hexagonal.Core.Ports.In;
using Hexagonal.Core.Ports.Out;

namespace Hexagonal.Core.UseCases;

public class GetOrderQueryService(IOrderRepository orders) : IGetOrderQuery
{
    public async Task<OrderView?> GetAsync(Guid orderId, CancellationToken ct = default) =>
        await orders.GetAsync(orderId, ct) is { } order ? ToView(order) : null;

    public async Task<IReadOnlyList<OrderView>> ListAsync(CancellationToken ct = default) =>
        (await orders.ListAsync(ct)).Select(ToView).ToList();

    private static OrderView ToView(Order o) => new(
        o.Id, o.CustomerId, o.Status.ToString(), o.Total, o.CreatedAt, o.PaymentTransactionId,
        o.Lines.Select(l => new OrderLineView(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice, l.LineTotal)).ToList());
}
