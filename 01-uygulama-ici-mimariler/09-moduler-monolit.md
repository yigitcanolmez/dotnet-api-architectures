# 9. Modüler Monolit (Modular Monolith)

> 💻 **Çalışan örnek proje:** [`samples/08-ModularMonolith`](../samples/08-ModularMonolith/)

> Simon Brown, Kamil Grzybek ve "Monolith First" (Martin Fowler) akımı; 2018+ yükselişte

## Neden Ortaya Çıktı?

2014–2019 arası "mikroservis her şeydir" dönemi yaşandı. Birçok ekip:
- 5 kişilik ekiple 30 mikroservis yönetmeye çalıştı.
- Dağıtık sistem karmaşıklığına (ağ, tutarlılık, deploy, gözlemlenebilirlik) boğuldu.
- **"Distributed Monolith"** ürettiler: servisler ayrı ama birbirine o kadar bağımlı ki birlikte deploy edilmek zorunda — iki dünyanın kötü yanları.
- Domain sınırlarını yanlış çizdi; servis sınırını değiştirmek çok pahalıydı.

Öte yandan klasik monolitler "Big Ball of Mud"a dönüşüyordu.

**Orta yol**: Tek deploy birimi, ama içeride **güçlü sınırlarla ayrılmış, bağımsız modüller.**

## Çözdüğü Problem

> "Mikroservislerin modülerlik faydalarını, dağıtık sistem maliyetini ödemeden elde et. Gerekirse ileride modülü servise çıkarmayı kolaylaştır."

## Yapı

```
┌──────────────────────── Tek Process / Tek Deploy ─────────────────────────┐
│                                                                            │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐   ┌─────────────┐ │
│  │   Ordering   │   │   Catalog    │   │   Payments   │   │  Shipping   │ │
│  │   Module     │   │   Module     │   │   Module     │   │  Module     │ │
│  │ ──────────── │   │ ──────────── │   │ ──────────── │   │ ─────────── │ │
│  │ Public API   │◄──┤ (sadece      │   │              │   │             │ │
│  │ (Contracts)  │   │  contracts)  │   │              │   │             │ │
│  │ Internal:    │   │ Internal:    │   │ Internal:    │   │ Internal:   │ │
│  │ Domain, App, │   │ VSA / CRUD   │   │ Clean + DDD  │   │ ...         │ │
│  │ Infra        │   │              │   │              │   │             │ │
│  │ Schema: ord  │   │ Schema: cat  │   │ Schema: pay  │   │ Schema: shp │ │
│  └──────┬───────┘   └──────┬───────┘   └──────┬───────┘   └──────┬──────┘ │
│         └─────── In-memory Event Bus / Outbox (async) ───────────┘        │
│                                                                            │
│                      Host (API, DI, Auth, Middleware)                      │
└────────────────────────────────────────────────────────────────────────────┘
                     Tek DB, ama modül başına ayrı SCHEMA
```

## Temel Kurallar

1. **Her modül bir Bounded Context'tir.**
2. Modüller birbirinin **iç sınıflarına erişemez** — sadece `*.Contracts` / `*.PublicApi` projesine.
3. **Veri izolasyonu**: Her modülün kendi DB schema'sı / DbContext'i var. Başka modülün tablosuna JOIN **yasak**.
4. Modüller arası iletişim:
   - **Senkron**: Public API arayüzü (in-process çağrı).
   - **Asenkron** (tercih edilen): Integration event'ler (in-memory bus veya outbox + broker).
5. Her modül kendi iç mimarisini seçebilir (biri Clean+DDD, diğeri basit CRUD/VSA).

## .NET Solution Yapısı

```
src/
├── Bootstrapper/
│   └── MyApp.Api/                       Program.cs — tüm modülleri yükler
├── Modules/
│   ├── Ordering/
│   │   ├── MyApp.Modules.Ordering/              (internal sınıflar)
│   │   │   ├── Features/ (VSA) veya Domain/Application/Infrastructure
│   │   │   ├── OrderingDbContext.cs  (HasDefaultSchema("ordering"))
│   │   │   └── OrderingModule.cs     (AddOrderingModule / MapOrderingEndpoints)
│   │   └── MyApp.Modules.Ordering.Contracts/    (public: IOrderingApi, integration events)
│   ├── Catalog/
│   └── Payments/
└── Shared/
    ├── MyApp.Shared.Abstractions/   (IEventBus, Result, IModule)
    └── MyApp.Shared.Infrastructure/ (outbox, event bus impl, auth)
```

## Kod Örneği

```csharp
// ---------- Catalog.Contracts (public) ----------
public interface ICatalogModuleApi
{
    Task<ProductSnapshot?> GetProductAsync(Guid productId, CancellationToken ct);
}
public record ProductSnapshot(Guid Id, string Name, decimal Price);
public record ProductPriceChanged(Guid ProductId, decimal NewPrice) : IIntegrationEvent;

// ---------- Catalog (internal) ----------
internal sealed class CatalogModuleApi(CatalogDbContext db) : ICatalogModuleApi
{
    public Task<ProductSnapshot?> GetProductAsync(Guid id, CancellationToken ct) =>
        db.Products.Where(p => p.Id == id).Select(p => new ProductSnapshot(p.Id, p.Name, p.Price)).FirstOrDefaultAsync(ct);
}

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection s, IConfiguration c)
    {
        s.AddDbContext<CatalogDbContext>(o => o.UseNpgsql(c.GetConnectionString("Db"),
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", "catalog")));
        s.AddScoped<ICatalogModuleApi, CatalogModuleApi>();
        return s;
    }
}

internal class CatalogDbContext(DbContextOptions<CatalogDbContext> o) : DbContext(o)
{
    protected override void OnModelCreating(ModelBuilder b) => b.HasDefaultSchema("catalog");
}

// ---------- Ordering, Catalog'u SADECE contract üzerinden kullanır ----------
internal sealed class PlaceOrderHandler(OrderingDbContext db, ICatalogModuleApi catalog) { /* ... */ }

// ---------- Ordering, Catalog event'ini dinler ----------
internal sealed class ProductPriceChangedHandler(OrderingDbContext db) : IIntegrationEventHandler<ProductPriceChanged> { /* ... */ }
```

### Sınırları zorlamak (Architecture Tests)

```csharp
[Fact]
public void Ordering_should_not_reference_Catalog_internals()
{
    var result = Types.InAssembly(typeof(OrderingModule).Assembly)
        .ShouldNot().HaveDependencyOn("MyApp.Modules.Catalog")      // Contracts hariç
        .GetResult();
    Assert.True(result.IsSuccessful);
}
```

Ayrıca modül içi sınıfları `internal` yapmak, derleyici seviyesinde koruma sağlar.

## Modül → Mikroservise Geçiş

Doğru kurulmuş bir modüler monolitte bir modülü ayırmak:
1. `ICatalogModuleApi` implementasyonunu in-process yerine **HTTP/gRPC client** ile değiştir.
2. In-memory event bus'ı **RabbitMQ/Kafka** ile değiştir (Outbox zaten varsa kolay).
3. Schema'yı ayrı DB'ye taşı.
4. Modülü ayrı host'ta çalıştır.

## Artıları

- ✅ Tek deploy, tek process → basit operasyon, kolay debug.
- ✅ In-process çağrılar → ağ gecikmesi/hata yok.
- ✅ Modül sınırları sonradan kolayca yeniden çizilebilir.
- ✅ Mikroservise evrimleşmeye hazır.
- ✅ Ekipler modül bazında sahiplik alabilir.
- ✅ Gerekirse tek DB transaction (ama dikkatli!).

## Eksileri

- ❌ Bağımsız ölçekleme yok (tüm uygulama birlikte ölçeklenir).
- ❌ Teknoloji çeşitliliği yok (hepsi .NET).
- ❌ Bir modüldeki memory leak / crash tüm uygulamayı etkiler.
- ❌ Sınırları korumak **disiplin** ister; aksi halde yine çamur topu.
- ❌ Tek deploy pipeline → büyük ekiplerde darboğaz.

## Ne Zaman Kullanılır?

- **Yeni projelerin çoğu için varsayılan başlangıç noktası** olarak önerilir.
- Domain sınırları henüz netleşmemişse.
- Ekip < ~30-50 geliştirici ise.
- Mikroservisin operasyonel maliyetini kaldıramayacaksanız.
