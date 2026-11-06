using Clean.Domain.Common;
using Clean.Domain.Orders;

namespace Clean.Domain.UnitTests.Orders;

public class MoneyTests
{
    [Fact]
    public void SameValues_AreEqual() // Value Object: kimlik değil değer eşitliği
    {
        Assert.Equal(new Money(10m, "TRY"), new Money(10m, "try"));
        Assert.NotEqual(new Money(10m, "TRY"), new Money(10m, "USD"));
    }

    [Fact]
    public void Add_SameCurrency_SumsAmounts()
    {
        var sum = new Money(10.5m, "TRY") + new Money(4.5m, "TRY");
        Assert.Equal(new Money(15m, "TRY"), sum);
    }

    [Fact]
    public void Add_DifferentCurrency_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => new Money(1m, "TRY") + new Money(1m, "USD"));
        Assert.Equal("Money.CurrencyMismatch", ex.Code);
    }

    [Fact]
    public void Multiply_ByQuantity()
    {
        Assert.Equal(new Money(1500m, "TRY"), new Money(750m, "TRY") * 2);
    }

    [Fact]
    public void Negative_Throws() => Assert.Throws<DomainException>(() => new Money(-1m, "TRY"));

    [Theory]
    [InlineData("")]
    [InlineData("TL")]
    [InlineData("EURO")]
    public void InvalidCurrency_Throws(string currency) =>
        Assert.Throws<DomainException>(() => new Money(1m, currency));
}
