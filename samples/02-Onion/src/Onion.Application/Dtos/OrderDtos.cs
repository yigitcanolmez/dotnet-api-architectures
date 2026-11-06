namespace Onion.Application.Dtos;

// [Application Services halkası] Dış dünyaya açılan veri sözleşmeleri. Domain entity'leri dışarı sızmaz.
public record PlaceOrderItemRequest(Guid ProductId, int Quantity);

public record PlaceOrderRequest(Guid CustomerId, List<PlaceOrderItemRequest>? Items);

public record OrderLineDto(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);

public record OrderDto(Guid Id, Guid CustomerId, string Status, decimal Total, DateTime CreatedAt, IReadOnlyList<OrderLineDto> Lines);

public record ProductDto(Guid Id, string Name, decimal Price, string Currency, int Stock);
