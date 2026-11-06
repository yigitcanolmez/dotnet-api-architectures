using Cqrs.Api.Write.Domain;

namespace Cqrs.Tests;

// Yazma modelinin birim testleri: kural + üretilen domain event'i.
public sealed class OrderAggregateTests
{
    private static readonly Product Keyboard = new(Guid.NewGuid(), "Klavye", 750m, 10);

    [Fact]
    public void Place_OrderPlaced_eventi_uretir()
    {
        var order = Order.Place(Guid.NewGuid(), [(Keyboard, 2)]);

        var placed = Assert.IsType<OrderPlaced>(Assert.Single(order.DomainEvents));
        Assert.Equal(1500m, placed.Total);
        Assert.Equal(["Klavye"], placed.ProductNames);
    }

    [Fact]
    public void Kargolanmis_siparis_iptal_edilemez()
    {
        var order = Order.Place(Guid.NewGuid(), [(Keyboard, 1)]);
        order.Ship();

        Assert.Throws<DomainException>(order.Cancel);
    }

    [Fact]
    public void Iptal_OrderCancelled_eventi_uretir()
    {
        var order = Order.Place(Guid.NewGuid(), [(Keyboard, 1)]);
        order.ClearDomainEvents();

        order.Cancel();

        Assert.IsType<OrderCancelled>(Assert.Single(order.DomainEvents));
        Assert.Throws<DomainException>(order.Cancel);
    }
}
