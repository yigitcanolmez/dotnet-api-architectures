namespace Layered.DataAccess.Entities;

// [Data Access Katmanı] Anemic entity: sadece property çantası, davranış yok.
// Herkes her property'yi değiştirebilir (public set) → kuralları korumak Service'lere kalır.
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "TRY";
    public int Stock { get; set; }
}
