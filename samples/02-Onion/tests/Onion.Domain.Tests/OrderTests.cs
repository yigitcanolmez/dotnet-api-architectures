using Onion.Domain.Entities;
using Onion.Domain.Exceptions;

namespace Onion.Domain.Tests;

// Saf domain testleri: veritabanı yok, DI yok, mock yok. Sadece `new` ve metot çağrısı.
// Karşılaştırın: 01-Layered/tests/Layered.Tests (aynı kural için SQLite kurulumu gerekiyordu).
public class OrderTests
{
    private static readonly Product Klavye = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Klavye", 750m, 10);
    private static readonly Product Mouse = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Mouse", 350m, 20);

    private static Order NewOrder() => Order.Place(Guid.NewGuid(), [(Klavye, 2), (Mouse, 1)], DateTime.UtcNow);

    [Fact]
    public void Place_creates_placed_order_and_calculates_total()
    {
        var order = NewOrder();

        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal(2, order.Lines.Count);
        Assert.Equal(1850m, order.Total);
    }

    [Fact]
    public void Place_requires_at_least_one_line() =>
        Assert.Throws<DomainException>(() => Order.Place(Guid.NewGuid(), [], DateTime.UtcNow));

    [Fact]
    public void Place_allows_at_most_ten_lines()
    {
        var items = Enumerable.Repeat((Klavye, 1), Order.MaxLines + 1).ToList();
        Assert.Throws<DomainException>(() => Order.Place(Guid.NewGuid(), items, DateTime.UtcNow));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Place_rejects_non_positive_quantity(int quantity) =>
        Assert.Throws<DomainException>(() => Order.Place(Guid.NewGuid(), [(Klavye, quantity)], DateTime.UtcNow));

    [Fact]
    public void Ship_changes_status_to_shipped()
    {
        var order = NewOrder();
        order.Ship();
        Assert.Equal(OrderStatus.Shipped, order.Status);
    }

    [Fact]
    public void Ship_is_only_allowed_for_placed_orders()
    {
        var order = NewOrder();
        order.Cancel();
        Assert.Throws<DomainException>(order.Ship);
    }

    [Fact]
    public void Cancel_is_not_allowed_after_shipping()
    {
        var order = NewOrder();
        order.Ship();

        var ex = Assert.Throws<DomainException>(order.Cancel);
        Assert.Contains("iptal edilemez", ex.Message);
    }

    [Fact]
    public void Cancel_twice_is_not_allowed()
    {
        var order = NewOrder();
        order.Cancel();
        Assert.Throws<DomainException>(order.Cancel);
    }
}
