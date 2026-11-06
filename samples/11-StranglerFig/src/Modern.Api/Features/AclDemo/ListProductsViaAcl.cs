using Modern.Api.Domain;

namespace Modern.Api.Features.AclDemo;

// Öğretim amaçlı: ACL'in çıktısını görmek için. Proxy bu yolu legacy'ye gönderir (catch-all),
// bu yüzden doğrudan http://localhost:5202/acl/products ile çağırın ve legacy'nin /api/products çıktısıyla karşılaştırın.
public static class ListProductsViaAcl
{
    public static void Map(IEndpointRouteBuilder app) => app.MapGet("/acl/products", async (IProductCatalog catalog, CancellationToken ct) =>
        Results.Ok(await catalog.GetAllAsync(ct)));
}
