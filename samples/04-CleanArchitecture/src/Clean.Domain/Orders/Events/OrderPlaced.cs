using Clean.Domain.Common;
using Clean.Domain.Products;

namespace Clean.Domain.Orders.Events;

// [Domain Event] Event, dinleyicilerin ihtiyaç duyduğu veriyi kendisi taşır (ürün + miktar),
// böylece Product aggregate'i Order'ı hiç tanımadan stok düşebilir.
public sealed record OrderPlaced(
    OrderId OrderId,
    CustomerId CustomerId,
    Money Total,
    IReadOnlyList<OrderPlacedLine> Lines) : IDomainEvent;

public sealed record OrderPlacedLine(ProductId ProductId, int Quantity);
