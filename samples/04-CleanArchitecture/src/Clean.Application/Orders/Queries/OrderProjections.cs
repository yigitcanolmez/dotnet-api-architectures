using System.Linq.Expressions;
using Clean.Domain.Orders;

namespace Clean.Application.Orders.Queries;

// Query tarafı aggregate'i yüklemez; doğrudan DTO'ya PROJEKSİYON yapar (AsNoTracking + Select).
// Not: strongly-typed ID'lerin .Value'su SQL'e çevrilemez; ancak en dış Select'te EF Core bunu
// sütunu okuduktan sonra istemci tarafında değerlendirir, bu yüzden güvenlidir.
internal static class OrderProjections
{
    public static readonly Expression<Func<Order, OrderDto>> ToDto = o => new OrderDto(
        o.Id.Value,
        o.CustomerId.Value,
        o.Status.ToString(),
        o.Total.Amount,
        o.Total.Currency,
        o.CreatedAtUtc,
        o.TrackingNumber,
        o.CancellationReason,
        o.Lines.Select(l => new OrderLineDto(l.ProductId.Value, l.ProductName, l.Quantity, l.UnitPrice.Amount)).ToList());
}
