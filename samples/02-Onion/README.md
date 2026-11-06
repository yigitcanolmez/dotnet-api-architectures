# 02 — Onion (Soğan) Mimarisi

📖 İlgili doküman: [../../01-uygulama-ici-mimariler/02-onion-mimarisi.md](../../01-uygulama-ici-mimariler/02-onion-mimarisi.md)

## Bu örnekte ne görüyorsunuz

- **Bağımlılıklar merkeze doğru**: `Onion.Domain` hiçbir projeye ve hiçbir NuGet paketine referans vermiyor.
- **Rich Domain Model**: `Order` private setter'lı; `Place()`, `Ship()`, `Cancel()` kuralları entity'nin içinde.
- **Dependency Inversion**: `IOrderRepository`/`IProductRepository` Domain'de, `INotificationService` Application'da tanımlı;
  implementasyonları en dış halkada (`Onion.Infrastructure`).
- EF Core eşlemeleri Domain'i kirletmiyor: `IEntityTypeConfiguration` + **backing field** (`_lines`).
- Üç test seviyesi: DB'siz saf **domain testleri**, el yazımı fake'lerle **application testleri**, NetArchTest ile **mimari testleri**.
- `Onion.Api` Minimal API + composition root; Infrastructure'ı sadece DI kaydı için biliyor.

## Klasör / dosya ağacı

```
02-Onion/
├── Onion.slnx
├── src/
│   ├── Onion.Domain/                         ● MERKEZ — referans: YOK
│   │   ├── Entities/Order.cs                 rich model: Place/Ship/Cancel, private set, _lines
│   │   ├── Entities/OrderLine.cs             Quantity > 0 kuralı, EF'e özgü alan yok
│   │   ├── Entities/Product.cs
│   │   ├── Entities/OrderStatus.cs
│   │   ├── Exceptions/DomainException.cs     (Api'de → 409)
│   │   ├── Interfaces/IOrderRepository.cs    ┐ Domain Services halkası:
│   │   ├── Interfaces/IProductRepository.cs  │ repository ARAYÜZLERİ
│   │   └── Services/OrderPlacementService.cs ┘ "bilinmeyen ürün" kuralı (Product + Order)
│   ├── Onion.Application/                    ◎ referans: Domain
│   │   ├── Orders/OrderAppService.cs         use case orkestrasyonu (kural YOK)
│   │   ├── Products/ProductAppService.cs
│   │   ├── Interfaces/INotificationService.cs
│   │   ├── Dtos/OrderDtos.cs
│   │   └── Exceptions/NotFoundException.cs   (Api'de → 404)
│   ├── Onion.Infrastructure/                 ○ referans: Application, Domain, EF Core
│   │   ├── Persistence/AppDbContext.cs
│   │   ├── Persistence/Configurations/       OrderConfiguration (OwnsMany + backing field), ProductConfiguration (seed)
│   │   ├── Persistence/Repositories/         EfOrderRepository, EfProductRepository
│   │   ├── Notifications/LoggingNotificationService.cs
│   │   └── DependencyInjection.cs            AddInfrastructure(connectionString)
│   └── Onion.Api/                            ○ referans: Application, Infrastructure (sadece DI)
│       ├── Program.cs                        composition root
│       ├── Endpoints/OrderEndpoints.cs, ProductEndpoints.cs
│       ├── ErrorHandling/ExceptionToProblemDetailsHandler.cs
│       ├── appsettings.json                  Urls: http://localhost:5002
│       └── Onion.Api.http
└── tests/
    ├── Onion.Domain.Tests/                   saf domain testleri (DB yok)
    ├── Onion.Application.Tests/              Fakes/ (FakeOrderRepository, FakeProductRepository, FakeNotificationService)
    └── Onion.ArchitectureTests/              NetArchTest: bağımlılık kuralları
```

## Proje referans grafiği

```
   ┌──────────── Onion.Api (composition root) ───────────┐
   │                                                      │
   ▼                                                      ▼
Onion.Infrastructure ──────────► Onion.Application ──────► Onion.Domain   ◄── HİÇBİR ŞEYE BAĞIMLI DEĞİL
   │  (EF Core, SQLite,               (OrderAppService,       (Order, Product,
   │   EfOrderRepository,              INotificationService)   IOrderRepository,
   │   LoggingNotificationService)                             OrderPlacementService)
   └─────────────────────────────────────────────────────────►┘

            Bağımlılıklar daima MERKEZE doğru →●
```

Halka görünümü:

```
 ┌───────────── Infrastructure / Api / Tests ─────────────┐
 │  ┌──────────── Application Services ────────────────┐  │
 │  │  ┌───────────── Domain Services ──────────────┐  │  │
 │  │  │  (IOrderRepository, OrderPlacementService) │  │  │
 │  │  │      ┌──── Domain Model ────┐              │  │  │
 │  │  │      │ Order, OrderLine,    │              │  │  │
 │  │  │      │ Product, OrderStatus │              │  │  │
 │  │  │      └──────────────────────┘              │  │  │
 │  │  └────────────────────────────────────────────┘  │  │
 │  └──────────────────────────────────────────────────┘  │
 └────────────────────────────────────────────────────────┘
```

**Neden bu yönde?** Uygulamanın en kalıcı ve değerli kısmı iş kuralları ("kargolanan sipariş iptal edilemez").
Veritabanı, ORM, bildirim kanalı değişebilir. Bu yüzden **arayüzler içeride tanımlanır, implementasyonlar dışarıda**:
`EfOrderRepository : IOrderRepository` → ok Infrastructure'dan Domain'e. Katmanlı örnekteki
`Business → DataAccess` oku böylece tersine döner (Dependency Inversion).

| | 01-Layered | 02-Onion |
|---|---|---|
| Repository arayüzü nerede? | Yok (somut sınıf DataAccess'te) | Domain/Interfaces |
| İş kuralları nerede? | `OrderService` | `Order` entity'si + `OrderPlacementService` |
| "Kargolanan iptal edilemez" testi | SQLite in-memory gerekir | `new` + `order.Cancel()` |

## Bir isteğin yolculuğu: `POST /orders/{id}/cancel`

1. **`src/Onion.Api/Endpoints/OrderEndpoints.cs`** → `MapPost("/{id:guid}/cancel")` → `OrderAppService.CancelAsync(id)`.
2. **`src/Onion.Application/Orders/OrderAppService.cs`** → `orders.GetAsync(id)` — burada sadece
   **`IOrderRepository`** arayüzü (Domain'de) biliniyor.
3. Çalışma zamanında DI bu arayüz için **`src/Onion.Infrastructure/Persistence/Repositories/EfOrderRepository.cs`**'i verir
   (kayıt: `src/Onion.Infrastructure/DependencyInjection.cs`) → `AppDbContext` → SQLite (`onion.db`).
   `Persistence/Configurations/OrderConfiguration.cs` sayesinde kalemler `_lines` alanına doldurulur.
4. Application, kuralı **domain'e uygulatır**: **`src/Onion.Domain/Entities/Order.cs`** → `Cancel()`
   (`Shipped` ise `DomainException`).
5. `orders.SaveChangesAsync()` (EF değişikliği algılar) → `INotificationService.NotifyOrderCancelledAsync()` →
   **`src/Onion.Infrastructure/Notifications/LoggingNotificationService.cs`** log yazar.
6. Endpoint `204 NoContent` döner. `DomainException` fırlatıldıysa
   **`src/Onion.Api/ErrorHandling/ExceptionToProblemDetailsHandler.cs`** onu **409 ProblemDetails**'e çevirir
   (`NotFoundException` → 404).

## Çalıştırma

```bash
cd samples/02-Onion
dotnet build
dotnet test                                   # Domain + Application + Architecture testleri
dotnet run --project src/Onion.Api            # http://localhost:5002 (onion.db çalışma dizininde oluşur)
```

> Not: Öğretim amaçlı sadelik için **migration yok**; açılışta `EnsureCreated()` + `HasData` seed çalışır.

```bash
curl http://localhost:5002/products

curl -i -X POST http://localhost:5002/orders -H "Content-Type: application/json" \
  -d '{"customerId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","items":[{"productId":"11111111-1111-1111-1111-111111111111","quantity":2}]}'
# → 201 {"id":"<ID>"}

curl http://localhost:5002/orders/<ID>
curl -i -X POST http://localhost:5002/orders/<ID>/ship      # 204 (konsolda "📧 Bildirim ... kargolandı")
curl -i -X POST http://localhost:5002/orders/<ID>/cancel    # 409 (kargolanmış sipariş)
```

Tüm istekler: `src/Onion.Api/Onion.Api.http`.

## Deneyin

1. **Altyapıyı değiştirin:** `Onion.Infrastructure` içinde `InMemoryOrderRepository : IOrderRepository` yazın ve
   `AddInfrastructure`'da kaydı değiştirin. Domain ve Application'da tek satır değişti mi?
2. **Mimari testi kırın:** `Onion.Domain.csproj`'a `Microsoft.EntityFrameworkCore` paketi ekleyip `Order`'a
   `[Table("Orders")]` attribute'u koyun ve `dotnet test` çalıştırın. Hangi test, neden kırıldı?
3. **Yeni kural ekleyin:** "Toplam tutarı 50.000 TRY'yi aşan sipariş verilemez" kuralını ekleyin. Hangi dosyaya
   yazdınız? Testini DB'siz yazabildiniz mi? (İpucu: `Order.Place` + `Onion.Domain.Tests`.)

## Gözlemler (bu örnekte)

- ✅ İş kuralları tek yerde (`Order`) ve atlanamaz; `order.Status = ...` derlenmez.
- ✅ Domain testleri milisaniyeler içinde, DB'siz; Application testleri mock kütüphanesi olmadan fake'lerle.
- ✅ Mimari kurallar NetArchTest ile otomatik korunuyor.
- ❌ Katmanlı örneğe göre daha fazla proje/dosya ve tören (arayüz + implementasyon + DTO + mapping).
- ❌ Hâlâ **yatay** katmanlar: "sipariş iptali" özelliği 4 projeye dağılmış durumda.
- ℹ️ Basitlik için tüm domain kuralı ihlalleri 409 Conflict olarak döner.
