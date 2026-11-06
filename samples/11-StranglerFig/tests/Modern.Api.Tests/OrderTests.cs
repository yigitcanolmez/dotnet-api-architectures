using Modern.Api.Domain;

namespace Modern.Api.Tests;

public class OrderTests
{
    private static readonly Product Klavye = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Klavye", Money.Try(750m), true);
    private static readonly Product Faks = new(Guid.NewGuid(), "Faks", Money.Try(1200m), false);

    [Fact]
    public void Toplam_kalemlerden_hesaplanir()
    {
        var order = Order.Place(Guid.NewGuid(), [(Klavye, 2)]);
        Assert.Equal(Money.Try(1500m), order.Total);
        Assert.Equal(OrderStatus.Placed, order.Status);
    }

    [Fact]
    public void Bos_veya_gecersiz_siparis_reddedilir()
    {
        Assert.Throws<DomainException>(() => Order.Place(Guid.NewGuid(), []));
        Assert.Throws<DomainException>(() => Order.Place(Guid.NewGuid(), [(Klavye, 0)]));
        Assert.Throws<DomainException>(() => Order.Place(Guid.NewGuid(), [(Faks, 1)]));
    }

    [Fact]
    public void Kargolanmis_siparis_iptal_edilemez()
    {
        var order = Order.Place(Guid.NewGuid(), [(Klavye, 1)]);
        order.Ship();
        var ex = Assert.Throws<DomainException>(order.Cancel);
        Assert.True(ex.IsConflict);
    }

    [Fact]
    public void Iptal_edilmis_siparis_tekrar_iptal_edilemez()
    {
        var order = Order.Place(Guid.NewGuid(), [(Klavye, 1)]);
        order.Cancel();
        Assert.Throws<DomainException>(order.Cancel);
    }
}
