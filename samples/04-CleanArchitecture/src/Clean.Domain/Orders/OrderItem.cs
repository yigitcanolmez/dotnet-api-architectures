using Clean.Domain.Products;

namespace Clean.Domain.Orders;

// Order.Place factory'sine verilen girdi: ürün bilgisi (snapshot) + miktar.
public sealed record OrderItem(ProductId ProductId, string ProductName, Money UnitPrice, int Quantity);
