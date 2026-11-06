// [Ordering — Yerel Okuma Modeli] Ordering'in fiyat/isim KOPYASI.
// Sipariş alırken Inventory'ye senkron HTTP çağrısı yapmamak için (temporal coupling yok).
// Gerçek sistemde bu tablo Catalog/Inventory'nin "ProductPriceChanged" gibi event'leriyle güncellenir
// (event-carried state transfer). Bu örnekte basitlik için seed edilir.
namespace Ordering.Api.Domain;

public class CatalogItem
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
}
