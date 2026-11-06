using VerticalSlice.Api.Common;

namespace VerticalSlice.Api.Domain;

// [Domain] VSA "domain modeli kullanma" demez. Stok kuralı birden fazla dilimde
// (PlaceOrder, CancelOrder, RestockProduct) gerektiği için buraya "push down" edildi.
public sealed class Product
{
    private Product() { } // EF Core için

    public Product(Guid id, string name, decimal price, int stock)
    {
        Id = id;
        Name = name;
        Price = price;
        Stock = stock;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public int Stock { get; private set; }

    public Result DecreaseStock(int quantity)
    {
        if (quantity > Stock)
            return Error.Conflict($"'{Name}' için yetersiz stok. Mevcut: {Stock}, istenen: {quantity}.");

        Stock -= quantity;
        return Result.Success();
    }

    public Result Restock(int quantity)
    {
        if (quantity <= 0)
            return Error.Validation("Stok artış miktarı 0'dan büyük olmalı.");

        Stock += quantity;
        return Result.Success();
    }
}
