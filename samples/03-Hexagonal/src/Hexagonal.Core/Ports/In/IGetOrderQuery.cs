namespace Hexagonal.Core.Ports.In;

// [Inbound / Driving Port] Okuma tarafı. Domain entity'si değil, dışarıya uygun "view" döner.
public interface IGetOrderQuery
{
    Task<OrderView?> GetAsync(Guid orderId, CancellationToken ct = default);
    Task<IReadOnlyList<OrderView>> ListAsync(CancellationToken ct = default);
}

public record OrderView(Guid Id, Guid CustomerId, string Status, decimal Total, DateTime CreatedAt, string? PaymentTransactionId, IReadOnlyList<OrderLineView> Lines);

public record OrderLineView(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
