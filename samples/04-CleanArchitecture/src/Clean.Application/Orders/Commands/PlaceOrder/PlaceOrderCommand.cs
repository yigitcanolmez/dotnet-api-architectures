using MediatR;

namespace Clean.Application.Orders.Commands.PlaceOrder;

// [Application Katmanı – Use Case girdisi] "Müşteri sipariş verir" niyeti.
public sealed record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<PlaceOrderItem> Items) : IRequest<Guid>;

public sealed record PlaceOrderItem(Guid ProductId, int Quantity);
