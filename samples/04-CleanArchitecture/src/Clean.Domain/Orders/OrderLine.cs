using Clean.Domain.Common;
using Clean.Domain.Products;

namespace Clean.Domain.Orders;

// [Domain Katmanı] Aggregate içi Entity. Constructor'ı internal: yalnızca Order (root) oluşturabilir.
// Product'a nesne referansı YOK; ProductId + o anki ad/fiyatın SNAPSHOT'ı tutulur.
// Böylece ürünün fiyatı sonradan değişse de geçmiş siparişin tutarı değişmez.
public sealed class OrderLine : Entity<Guid>
{
    public ProductId ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public Money UnitPrice { get; private set; } = null!;
    public int Quantity { get; private set; }

    public Money SubTotal => UnitPrice * Quantity;

    private OrderLine() { } // EF Core için

    internal OrderLine(ProductId productId, string productName, Money unitPrice, int quantity)
    {
        if (quantity <= 0) throw new DomainException(OrderErrors.InvalidQuantity);

        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }
}
