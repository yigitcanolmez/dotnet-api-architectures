namespace Clean.Application.Orders.Queries;

// Use case çıktısı (Output Port verisi). Domain nesnesi dışarı SIZDIRILMAZ; API sadece DTO görür.
public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal Total,
    string Currency,
    DateTime CreatedAtUtc,
    string? TrackingNumber,
    string? CancellationReason,
    IReadOnlyList<OrderLineDto> Lines);

public sealed record OrderLineDto(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
