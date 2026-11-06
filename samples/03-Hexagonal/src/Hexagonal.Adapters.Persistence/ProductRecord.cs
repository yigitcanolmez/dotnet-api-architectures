namespace Hexagonal.Adapters.Persistence;

// Persistence adapter'ın KENDİ tablo modeli. Core'da Product entity'si yok; Core sadece
// IProductCatalog port'u üzerinden CatalogProduct görür. Eşleme bu adapter'ın içinde kalır.
public class ProductRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "TRY";
    public int Stock { get; set; }
}
