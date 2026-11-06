using Clean.Domain.Common;
using Clean.Domain.Orders;
using Clean.Domain.Products;

namespace Clean.Domain.UnitTests.Products;

public class ProductTests
{
    private static Product Keyboard(int stock = 10) =>
        Product.Create(new ProductId(Guid.NewGuid()), "Klavye", new Money(750m, "TRY"), stock);

    [Fact]
    public void DecreaseStock_Enough_Decreases()
    {
        var product = Keyboard(10);
        product.DecreaseStock(3);
        Assert.Equal(7, product.Stock);
    }

    [Fact]
    public void DecreaseStock_Insufficient_ThrowsAndKeepsStock()
    {
        var product = Keyboard(2);

        var ex = Assert.Throws<DomainException>(() => product.DecreaseStock(3));
        Assert.Equal("Product.InsufficientStock", ex.Code);
        Assert.Equal(2, product.Stock);
    }

    [Fact]
    public void IncreaseStock_Increases()
    {
        var product = Keyboard(1);
        product.IncreaseStock(4);
        Assert.Equal(5, product.Stock);
    }
}
