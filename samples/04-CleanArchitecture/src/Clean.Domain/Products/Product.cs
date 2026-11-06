using Clean.Domain.Common;
using Clean.Domain.Orders;

namespace Clean.Domain.Products;

// [Domain Katmanı] Product ayrı bir aggregate'tir; kendi tutarlılık kuralı (stok negatife düşemez) vardır.
// Order'ı tanımaz. Stok, OrderPlaced/OrderCancelled event'lerine tepki veren handler'lar tarafından değiştirilir.
public sealed class Product : AggregateRoot<ProductId>
{
    public string Name { get; private set; } = null!;
    public Money Price { get; private set; } = null!;
    public int Stock { get; private set; }

    private Product() { } // EF Core için

    public static Product Create(ProductId id, string name, Money price, int stock)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException(new Error("Product.NameRequired", "Ürün adı zorunludur."));
        if (stock < 0)
            throw new DomainException(new Error("Product.NegativeStock", "Stok negatif olamaz."));

        return new Product { Id = id, Name = name, Price = price, Stock = stock };
    }

    public void DecreaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException(new Error("Product.InvalidQuantity", "Miktar 0'dan büyük olmalı."));
        if (Stock < quantity)
            throw new DomainException(new Error("Product.InsufficientStock",
                $"'{Name}' için yetersiz stok. Mevcut: {Stock}, istenen: {quantity}."));

        Stock -= quantity;
    }

    public void IncreaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException(new Error("Product.InvalidQuantity", "Miktar 0'dan büyük olmalı."));

        Stock += quantity;
    }
}
