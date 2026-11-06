namespace Hexagonal.Core.Ports.In;

// [Inbound / Driving Port] Çekirdeğin dışarıya sunduğu use case.
// Driving adapter'lar: Web (OrdersEndpoints), Messaging (PlaceOrderConsumer), testler.
public interface IPlaceOrderUseCase
{
    Task<Guid> ExecuteAsync(PlaceOrderCommand command, CancellationToken ct = default);
}

public record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<PlaceOrderItem> Items);

public record PlaceOrderItem(Guid ProductId, int Quantity);
