using Clean.Domain.Common;
using Clean.Domain.Orders;
using Clean.Domain.Orders.Events;
using static Clean.Domain.UnitTests.Orders.OrderTestData;

namespace Clean.Domain.UnitTests.Orders;

// Domain testleri: veritabanı, DI, mock yok. Saf C# nesneleri → milisaniyeler içinde koşar.
public class OrderTests
{
    [Fact]
    public void Place_ValidItems_CreatesPlacedOrderWithTotal()
    {
        var order = PlacedOrder();

        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal(2, order.Lines.Count);
        Assert.Equal(new Money(1850m, "TRY"), order.Total);
        Assert.Equal(Now, order.CreatedAtUtc);
    }

    [Fact]
    public void Place_RaisesOrderPlacedEvent()
    {
        var order = PlacedOrder();

        var placed = Assert.IsType<OrderPlaced>(Assert.Single(order.DomainEvents));
        Assert.Equal(order.Id, placed.OrderId);
        Assert.Equal(order.Total, placed.Total);
        Assert.Equal(2, placed.Lines.Count);
    }

    [Fact]
    public void Place_NoItems_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => Order.Place(Customer, [], Now));
        Assert.Equal(OrderErrors.NoLines, ex.Error);
    }

    [Fact]
    public void Place_MoreThanTenItems_Throws()
    {
        var items = Enumerable.Range(0, 11).Select(_ => Item()).ToList();

        var ex = Assert.Throws<DomainException>(() => Order.Place(Customer, items, Now));
        Assert.Equal(OrderErrors.TooManyLines, ex.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Place_NonPositiveQuantity_Throws(int quantity)
    {
        var ex = Assert.Throws<DomainException>(() => Order.Place(Customer, [Item(quantity)], Now));
        Assert.Equal(OrderErrors.InvalidQuantity, ex.Error);
    }

    [Fact]
    public void Place_MixedCurrencies_Throws()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Order.Place(Customer, [Item(1, 10m, "TRY"), Item(1, 10m, "USD")], Now));
        Assert.Equal("Money.CurrencyMismatch", ex.Code);
    }

    [Fact]
    public void Ship_PlacedOrder_SetsShippedAndRaisesEvent()
    {
        var order = PlacedOrder();
        order.ClearDomainEvents();

        order.Ship("TRK-1");

        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal("TRK-1", order.TrackingNumber);
        var shipped = Assert.IsType<OrderShipped>(Assert.Single(order.DomainEvents));
        Assert.Equal("TRK-1", shipped.TrackingNumber);
    }

    [Fact]
    public void Ship_CancelledOrder_Throws()
    {
        var order = PlacedOrder();
        order.Cancel("vazgeçti");

        var ex = Assert.Throws<DomainException>(() => order.Ship("TRK-1"));
        Assert.Equal(OrderErrors.OnlyPlacedCanBeShipped, ex.Error);
    }

    [Fact]
    public void Ship_AlreadyShipped_Throws()
    {
        var order = PlacedOrder();
        order.Ship("TRK-1");

        Assert.Throws<DomainException>(() => order.Ship("TRK-2"));
    }

    [Fact]
    public void Cancel_PlacedOrder_SetsCancelledAndRaisesEvent()
    {
        var order = PlacedOrder();
        order.ClearDomainEvents();

        order.Cancel("Müşteri vazgeçti");

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        var cancelled = Assert.IsType<OrderCancelled>(Assert.Single(order.DomainEvents));
        Assert.Equal("Müşteri vazgeçti", cancelled.Reason);
        Assert.Equal(2, cancelled.Lines.Count);
    }

    [Fact]
    public void Cancel_ShippedOrder_Throws()
    {
        var order = PlacedOrder();
        order.Ship("TRK-1");

        var ex = Assert.Throws<DomainException>(() => order.Cancel("geç kaldı"));
        Assert.Equal(OrderErrors.ShippedCannotBeCancelled, ex.Error);
        Assert.Equal(OrderStatus.Shipped, order.Status);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_Throws()
    {
        var order = PlacedOrder();
        order.Cancel("ilk");

        var ex = Assert.Throws<DomainException>(() => order.Cancel("ikinci"));
        Assert.Equal(OrderErrors.AlreadyCancelled, ex.Error);
    }

    [Fact]
    public void CanCancel_ReturnsResultWithoutThrowing()
    {
        var order = PlacedOrder();
        Assert.True(order.CanCancel().IsSuccess);

        order.Ship("TRK-1");
        var result = order.CanCancel();

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.ShippedCannotBeCancelled, result.Error);
    }

    [Fact]
    public void ClearDomainEvents_EmptiesEventList()
    {
        var order = PlacedOrder();
        order.ClearDomainEvents();
        Assert.Empty(order.DomainEvents);
    }
}
