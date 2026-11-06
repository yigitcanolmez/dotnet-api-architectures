# 01 — Katmanlı Mimari (N-Tier / Layered)

📖 İlgili doküman: [../../01-uygulama-ici-mimariler/01-katmanli-mimari.md](../../01-uygulama-ici-mimariler/01-katmanli-mimari.md)

## Bu örnekte ne görüyorsunuz

- Klasik 3 katman: **Presentation** (`Layered.Api`) → **Business** (`Layered.Business`) → **Data Access** (`Layered.DataAccess`).
- **Anemic Domain Model**: `Order`, `OrderLine`, `Product` sadece `public set` property'leri olan POCO'lar; davranış yok.
- **Transaction Script**: Tüm iş kuralları (`en az 1 / en fazla 10 kalem`, `quantity > 0`, `kargolanan iptal edilemez`…) tek bir `OrderService` içinde.
- Repository'ler **somut sınıf** (interface yok) — bilinçli. Klasik katmanlı projelerin çoğu böyle başlar.
- Klasik `[ApiController]` controller'lar ve exception → `ProblemDetails` çeviren bir middleware.
- Ve en önemlisi: **bu mimarinin sorunu** — iş katmanı veri erişimine bağımlı, test için veritabanı şart.

## Klasör / dosya ağacı

```
01-Layered/
├── Layered.slnx
├── src/
│   ├── Layered.DataAccess/                 ← EN ALT KATMAN (EF Core + SQLite)
│   │   ├── Entities/Order.cs               anemic entity, public set, EF navigation
│   │   ├── Entities/OrderLine.cs           EF'in istediği şekil (Id, OrderId FK, Order nav)
│   │   ├── Entities/Product.cs
│   │   ├── Entities/OrderStatus.cs         enum bile DataAccess'te!
│   │   ├── AppDbContext.cs                 DbContext + seed
│   │   ├── SeedData.cs                     3 sabit ürün
│   │   └── Repositories/
│   │       ├── OrderRepository.cs          somut sınıf (interface yok)
│   │       └── ProductRepository.cs
│   ├── Layered.Business/                   ← İŞ KATMANI (DataAccess'e bağımlı!)
│   │   ├── Services/OrderService.cs        TÜM iş kuralları (transaction script)
│   │   ├── Services/ProductService.cs      "pass-through" servis
│   │   ├── Dtos/OrderDtos.cs
│   │   └── Exceptions/                     BusinessException (→409), NotFoundException (→404)
│   └── Layered.Api/                        ← SUNUM KATMANI
│       ├── Controllers/OrdersController.cs
│       ├── Controllers/ProductsController.cs
│       ├── Middleware/ExceptionHandlingMiddleware.cs   exception → ProblemDetails
│       ├── Program.cs                      DI kayıtları, EnsureCreated
│       ├── appsettings.json                Urls: http://localhost:5001
│       └── Layered.Api.http                örnek istekler
└── tests/
    └── Layered.Tests/
        ├── SqliteTestDatabase.cs           iş kuralı testi için bile SQLite in-memory kurulumu
        └── OrderServiceTests.cs
```

## Proje referans grafiği

```
   Layered.Api  ───────►  Layered.Business  ───────►  Layered.DataAccess  ───────►  EF Core / SQLite
  (Controllers)           (OrderService)              (Entities, DbContext,
                                                       Repositories)
                     Bağımlılık yönü: YUKARIDAN AŞAĞIYA ↓

   Layered.Tests ──► Layered.Business + Layered.DataAccess + EF Core Sqlite (!)
```

**Neden bu yönde?** Katmanlı mimarinin kuralı "her katman sadece altındakini bilir". Sunum, iş katmanını çağırır;
iş katmanı veriyi okumak için veri erişimini çağırır. Akış doğal ama **en değerli kod (iş kuralları), en değişken
koda (veritabanı/ORM) bağımlı** hâle gelir.

### ⚠️ Bu mimarinin sorunu — örnekte açıkça görün

| Sorun | Örnekte nerede? |
|---|---|
| **Business, DataAccess'e bağımlı** (DIP ihlali) | `Layered.Business.csproj` → `ProjectReference ..\Layered.DataAccess`; `OrderService(OrderRepository, ProductRepository)` somut tipler alıyor. |
| **Entity'ler EF'e göre şekillenmiş** | `OrderLine` içinde `Id`, `OrderId` (FK) ve `Order` navigation'ı sırf EF ilişkisi için var; `OrderStatus` enum'u bile DataAccess'te. |
| **Anemic model, kurallar korunmuyor** | `order.Status = OrderStatus.Cancelled;` her yerden yazılabilir. Testte bile `Status = Shipped` doğrudan atanıyor. |
| **OrderService DB olmadan unit test edilemez** | `tests/Layered.Tests/SqliteTestDatabase.cs`: "kargolanan iptal edilemez" kuralını test etmek için SQLite in-memory bağlantısı, şema ve seed kurmak gerekiyor. |
| **God service** | Her yeni kural `OrderService`'e ekleniyor. |
| **Pass-through kod** | `ProductsController → ProductService → ProductRepository` hiçbir mantık eklemeden çağrı iletiyor. |

Bu sorunlar Onion / Hexagonal / Clean mimarilerin doğuş sebebidir → [../02-Onion](../02-Onion) ile karşılaştırın.

## Bir isteğin yolculuğu: `POST /orders/{id}/cancel`

1. **`src/Layered.Api/Middleware/ExceptionHandlingMiddleware.cs`** — istek pipeline'a girer, `try` bloğu açılır.
2. **`src/Layered.Api/Controllers/OrdersController.cs`** → `Cancel(Guid id)` → `service.CancelAsync(id)`.
3. **`src/Layered.Business/Services/OrderService.cs`** → `CancelAsync`:
   - `orders.GetAsync(id)` çağırır →
4. **`src/Layered.DataAccess/Repositories/OrderRepository.cs`** → `db.Orders.Include(o => o.Lines)...` →
   **`src/Layered.DataAccess/AppDbContext.cs`** → SQLite (`layered.db`).
5. Geri `OrderService`'e dönülür: iş kuralı **serviste** kontrol edilir
   (`if (order.Status == OrderStatus.Shipped) throw new BusinessException(...)`), sonra `order.Status = Cancelled` atanır
   ve `orders.SaveChangesAsync()` çağrılır.
6. Controller `204 NoContent` döner. Kural ihlal edildiyse `BusinessException` middleware'e kadar yükselir ve
   **409 ProblemDetails** olarak yazılır (bulunamadıysa `NotFoundException` → 404).

## Çalıştırma

```bash
cd samples/01-Layered
dotnet build
dotnet test
dotnet run --project src/Layered.Api      # http://localhost:5001 (layered.db çalışma dizininde oluşur)
```

> Not: Öğretim amaçlı sadelik için **migration yok**; uygulama açılışta `EnsureCreated()` çalıştırır ve
> ürünler `HasData` ile seed edilir. Şemayı değiştirirseniz `layered.db` dosyasını silin.

Örnek `curl` komutları:

```bash
curl http://localhost:5001/products

curl -i -X POST http://localhost:5001/orders -H "Content-Type: application/json" \
  -d '{"customerId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","items":[{"productId":"11111111-1111-1111-1111-111111111111","quantity":2}]}'
# → 201 {"id":"<ID>"}

curl http://localhost:5001/orders/<ID>
curl -i -X POST http://localhost:5001/orders/<ID>/ship      # 204
curl -i -X POST http://localhost:5001/orders/<ID>/cancel    # 409 (kargolanmış sipariş)
```

Tüm istekler için: `src/Layered.Api/Layered.Api.http`.

## Deneyin

1. **Kuralı atlayın:** `ProductService` içine `OrderRepository` enjekte edip kargolanmış bir siparişin `Status`'ünü
   `Cancelled` yapan bir metot yazın. Derleyici sizi durduruyor mu? (Hayır — anemic modelin bedeli.) Sonra aynı şeyi
   `02-Onion`'da deneyin.
2. **DB'siz test yazmaya çalışın:** `SqliteTestDatabase` kullanmadan `OrderService.CancelAsync` için bir test yazın.
   Neye ihtiyaç duyduğunuzu not edin (interface? sanal metot? mock kütüphanesi?).
3. **Veritabanını değiştirin:** SQLite yerine `UseInMemoryDatabase` kullanmak istediğinizde hangi projelere
   dokunmanız gerektiğini sayın. Business'ı yeniden derlemek gerekiyor mu?

## Gözlemler (bu örnekte)

- ✅ Çok az dosya, anlaşılması kolay; her geliştirici ilk bakışta nereye ne yazılacağını biliyor.
- ✅ CRUD ağırlıklı küçük uygulamalar için hızlı.
- ❌ 3 testin hepsi aslında integration test; sadece iş kuralını doğrulamak için şema + seed kuruluyor.
- ❌ İş kuralları entity'nin dışında; korunmaları geliştiricinin disiplinine kalmış.
- ❌ `OrderService` her yeni özellikle büyüyecek; tüm katmanlara dokunmadan özellik eklenemiyor.
- ℹ️ Basitlik için tüm iş kuralı ihlalleri 409 Conflict olarak döner.
