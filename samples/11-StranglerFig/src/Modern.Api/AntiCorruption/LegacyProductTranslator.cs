using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Modern.Api.Domain;

namespace Modern.Api.AntiCorruption;

// [Anti-Corruption Layer] Legacy.Api'nin HTTP API'sini çağırır ve legacy modelini
// modern domain modeline (Product, Money) ÇEVİRİR. Domain'in istediği port: IProductCatalog.
//   PRD_NM "MONITOR"   → Name "Monitor"      (BÜYÜK HARF → okunur isim)
//   PRC    "6.500,00"  → Money(6500, "TRY")  (Türkçe biçimli string → decimal + para birimi)
//   STAT_CD "A" / "P"  → IsSellable true / false
// Ürünler taşındığında sadece bu sınıfın yerine yeni bir IProductCatalog yazılır; domain değişmez.
public sealed class LegacyProductTranslator(HttpClient http, ILogger<LegacyProductTranslator> logger) : IProductCatalog
{
    private static readonly NumberFormatInfo LegacyNumberFormat = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };
    private static readonly JsonSerializerOptions LegacyJson = new() { PropertyNameCaseInsensitive = false };

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct)
    {
        var legacy = await http.GetFromJsonAsync<List<LegacyProductDto>>("/api/products", LegacyJson, ct) ?? [];
        var products = new List<Product>(legacy.Count);
        foreach (var dto in legacy)
        {
            if (TryTranslate(dto, out var product)) products.Add(product);
            else logger.LogWarning("ACL: çevrilemeyen legacy ürün kaydı atlandı: {@Dto}", dto);
        }
        return products;
    }

    internal static bool TryTranslate(LegacyProductDto dto, out Product product)
    {
        product = null!;
        if (!Guid.TryParse(dto.PRD_ID, out var id)) return false;
        if (!decimal.TryParse(dto.PRC, NumberStyles.Number, LegacyNumberFormat, out var amount)) return false;

        var isSellable = dto.STAT_CD switch
        {
            "A" => true,   // aktif
            "P" => false,  // pasif (satıştan kalkmış)
            _ => false     // bilinmeyen kodlar güvenli tarafta: satılamaz
        };

        product = new Product(id, ToDisplayName(dto.PRD_NM), Money.Try(amount), isSellable);
        return true;
    }

    // Legacy isimler büyük harf ve Türkçe karaktersiz: "MONITOR" → "Monitör" gibi düzeltmeler burada yapılır.
    private static readonly Dictionary<string, string> KnownNames = new()
    {
        ["KLAVYE"] = "Klavye",
        ["MOUSE"] = "Mouse",
        ["MONITOR"] = "Monitör"
    };

    private static string ToDisplayName(string legacyName) =>
        KnownNames.TryGetValue(legacyName, out var name)
            ? name
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(legacyName.ToLowerInvariant());
}
