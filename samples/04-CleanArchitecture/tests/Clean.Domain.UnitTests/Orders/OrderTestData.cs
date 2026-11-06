using Clean.Domain.Orders;
using Clean.Domain.Products;

namespace Clean.Domain.UnitTests.Orders;

internal static class OrderTestData
{
    public static readonly CustomerId Customer = new(Guid.NewGuid());
    public static readonly DateTime Now = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public static OrderItem Item(int quantity = 1, decimal price = 100m, string currency = "TRY") =>
        new(new ProductId(Guid.NewGuid()), "Ürün", new Money(price, currency), quantity);

    public static Order PlacedOrder() => Order.Place(Customer, [Item(2, 750m), Item(1, 350m)], Now);
}
