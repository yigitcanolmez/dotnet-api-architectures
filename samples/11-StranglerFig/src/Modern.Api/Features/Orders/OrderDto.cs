using Modern.Api.Domain;

namespace Modern.Api.Features.Orders;

public sealed record MoneyDto(decimal Amount, string Currency);
public sealed record OrderLineDto(Guid ProductId, string ProductName, int Quantity, MoneyDto UnitPrice);
public sealed record OrderDto(Guid Id, Guid CustomerId, string Status, MoneyDto Total, DateTime CreatedAt, IReadOnlyList<OrderLineDto> Lines)
{
    public static OrderDto From(Order o) => new(
        o.Id, o.CustomerId, o.Status.ToString(), new(o.Total.Amount, o.Total.Currency), o.CreatedAt,
        o.Lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.Quantity, new(l.UnitPrice, l.Currency))).ToList());
}
