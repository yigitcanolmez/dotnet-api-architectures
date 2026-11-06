namespace Onion.Domain.Entities;

// [Domain Model] Ürün. Durumu sadece kendi metotlarıyla değişebilir (private set).
public class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public int Stock { get; private set; }

    private Product() { } // EF Core materialization için

    public Product(Guid id, string name, decimal price, int stock, string currency = "TRY")
    {
        if (string.IsNullOrWhiteSpace(name)) throw new Exceptions.DomainException("Ürün adı boş olamaz.");
        if (price < 0) throw new Exceptions.DomainException("Fiyat negatif olamaz.");
        Id = id;
        Name = name;
        Price = price;
        Stock = stock;
        Currency = currency;
    }
}
