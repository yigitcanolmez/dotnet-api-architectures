# 08 — Modüler Monolit (Modular Monolith)

📖 İlgili dokümanlar:
- [Modüler Monolit](../../01-uygulama-ici-mimariler/09-moduler-monolit.md) (ana doküman)
- [Event-Driven Architecture](../../02-dagitik-mimariler/03-event-driven-architecture.md) (integration event, event-carried state transfer)
- [Transactional Outbox & Inbox](../../03-dagitik-desenler/02-outbox-inbox.md)

Tek process, tek deploy — ama içeride **Catalog**, **Ordering** ve **Shipping** adında, birbirinin iç sınıflarını
göremeyen, kendi veritabanına sahip üç modül.

## Bu örnekte ne görüyorsunuz

- **Bootstrapper** (`Program.cs`) sadece `AddXxxModule()` / `MapXxxEndpoints()` çağırır; iş mantığı yok.
- **Modül = Bounded Context**: her modülün implementasyonu `internal`; dışarıya açık olan tek şey `XxxModule`
  static sınıfı ve `*.Contracts` projesi. Bu kural **NetArchTest** ile test ediliyor.
- **Veri izolasyonu**: her modülün kendi `DbContext`'i ve kendi SQLite dosyası (`catalog.db`, `ordering.db`, `shipping.db`).
  Modüller arası JOIN imkânsız.
- **Senkron iletişim**: Ordering, sipariş verirken ürün adı/fiyatını `ICatalogModuleApi` (Catalog.Contracts) üzerinden in-process sorar.
- **Asenkron iletişim**: Ordering `OrderPlacedIntegrationEvent`'i siparişle **aynı transaction'da outbox'a** yazar;
  `OutboxProcessor<TDbContext>` bunu in-memory bus'a basar; Catalog stoğu düşer, Shipping kargo kaydı açar.
- **Inbox / idempotency**: her handler işlediği event Id'sini kendi modülünün `InboxMessages` tablosuna,
  iş verisiyle aynı `SaveChanges` içinde yazar → aynı event iki kez gelse de bir kez işlenir.

## Modül sınırı diyagramı

```
┌──────────────────────────── Tek Process (ModularMonolith.Api :5008) ─────────────────────────────┐
│                                                                                                  │
│  ┌──────── Catalog ─────────┐     ┌──────── Ordering ────────┐     ┌──────── Shipping ────────┐  │
│  │ Contracts (public):      │     │ Contracts (public):      │     │ Contracts (public):      │  │
│  │  ICatalogModuleApi ◄─────┼─────┼── PlaceOrder (SENKRON)   │     │  ShipmentCreated         │  │
│  │  StockReserved           │     │  OrderPlaced             │     │                          │  │
│  │  StockReservationFailed  │     │  OrderShipped            │     │                          │  │
│  │ ──────────────────────── │     │  OrderCancelled          │     │ ──────────────────────── │  │
│  │ internal: VSA/CRUD       │     │ ──────────────────────── │     │ internal: VSA            │  │
│  │  Product, Reservation    │     │ internal: zengin domain  │     │  Shipment                │  │
│  │ catalog.db               │     │  Order, OrderLine (VSA)  │     │ shipping.db              │  │
│  │  + Outbox + Inbox        │     │ ordering.db+Outbox+Inbox │     │  + Outbox + Inbox        │  │
│  └───────────▲──────┬───────┘     └──────┬──────────▲────────┘     └───────────▲──────────────┘  │
│              │      │ outbox             │ outbox   │                          │                 │
│   ═══════════╧══════╧════════ In-memory Event Bus (IEventBus) ═════╧═══════════╧═════════════    │
│     OrderPlaced ──► Catalog (stok düş)  +  Shipping (kargo aç)                                   │
│     OrderCancelled ──► Catalog (stok iade) + Shipping (kargo iptal)                              │
│     StockReservationFailed ──► Ordering (siparişi iptal et)                                      │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
  Üretimde: tek veritabanı, modül başına ayrı SCHEMA (catalog / ordering / shipping).
  SQLite schema desteklemediği için bu örnekte modül başına AYRI .db dosyası kullanılıyor.
```

## Klasör / dosya ağacı

```
08-ModularMonolith/
├── ModularMonolith.slnx
├── src/
│   ├── Bootstrapper/ModularMonolith.Api/
│   │   ├── Program.cs                  # Sadece AddXxxModule() + MapXxxEndpoints()
│   │   ├── appsettings.json            # Port 5008 + modül başına connection string
│   │   └── ModularMonolith.Api.http    # Tüm endpoint'ler için örnek istekler
│   ├── Shared/
│   │   ├── ModularMonolith.Shared.Abstractions/
│   │   │   ├── Events/IIntegrationEvent.cs, IIntegrationEventHandler.cs, IEventBus.cs
│   │   │   └── Results/Result.cs       # Result / Error (Validation, NotFound, Conflict)
│   │   └── ModularMonolith.Shared.Infrastructure/
│   │       ├── Persistence/ModuleDbContext.cs     # Her modül DbContext'inin tabanı: OutboxMessages + InboxMessages
│   │       ├── Persistence/DatabaseInitializer.cs # EnsureCreated + seed (hosted service)
│   │       ├── Outbox/OutboxMessage.cs, OutboxProcessor.cs  # Generic OutboxProcessor<TDbContext> (BackgroundService)
│   │       ├── Inbox/InboxMessage.cs, IdempotentIntegrationEventHandler.cs
│   │       ├── Messaging/InMemoryEventBus.cs      # Handler'ları DI'dan çözüp kendi scope'larında çağırır
│   │       ├── Modules/ModuleRegistrationExtensions.cs  # AddModuleDbContext / AddIntegrationEventHandler
│   │       └── Http/ErrorExtensions.cs            # Error → ProblemDetails (400/404/409)
│   └── Modules/
│       ├── Catalog/
│       │   ├── ModularMonolith.Modules.Catalog.Contracts/   # ICatalogModuleApi, ProductSnapshot, Stock* event'leri
│       │   └── ModularMonolith.Modules.Catalog/             # (internal)
│       │       ├── CatalogModule.cs                         # TEK public tip
│       │       ├── Api/CatalogModuleApi.cs                  # ICatalogModuleApi implementasyonu
│       │       ├── Domain/Product.cs, StockReservation.cs
│       │       ├── Infrastructure/CatalogDbContext.cs, CatalogSeed.cs
│       │       └── Features/ListProducts, ReserveStock (OrderPlaced handler), ReleaseStock (OrderCancelled handler)
│       ├── Ordering/
│       │   ├── ModularMonolith.Modules.Ordering.Contracts/  # OrderPlaced/OrderShipped/OrderCancelled event'leri
│       │   └── ModularMonolith.Modules.Ordering/            # (internal)
│       │       ├── OrderingModule.cs
│       │       ├── Domain/Order.cs                          # Zengin domain: Place / Ship / Cancel invariant'ları
│       │       ├── Infrastructure/OrderingDbContext.cs
│       │       └── Features/PlaceOrder, GetOrder, ListOrders, ShipOrder, CancelOrder,
│       │                    CancelOrderOnStockReservationFailed (Catalog event handler'ı)
│       └── Shipping/
│           ├── ModularMonolith.Modules.Shipping.Contracts/  # ShipmentCreated (henüz dinleyeni yok)
│           └── ModularMonolith.Modules.Shipping/            # (internal)
│               ├── ShippingModule.cs
│               ├── Domain/Shipment.cs                       # AwaitingShipment → Shipped / Cancelled
│               ├── Infrastructure/ShippingDbContext.cs
│               └── Features/ListShipments, GetShipmentByOrder, CreateShipment, MarkShipmentShipped, CancelShipment
└── tests/
    ├── ModularMonolith.ArchitectureTests/ModuleBoundaryTests.cs   # NetArchTest ile sınır kuralları
    └── ModularMonolith.IntegrationTests/OrderFlowTests.cs         # WebApplicationFactory + polling
```

## Proje referans grafiği

```
                        ModularMonolith.Api  (Bootstrapper / composition root)
                 ┌────────────────────┼────────────────────┐
                 ▼                    ▼                    ▼
          Modules.Catalog      Modules.Ordering      Modules.Shipping        (internal implementasyonlar)
                 │                    │                    │
                 ▼                    ▼                    ▼
        Catalog.Contracts     Ordering.Contracts    Shipping.Contracts       (public sözleşmeler)
                 │                    │                    │
                 └────────────────────┼────────────────────┘
                                      ▼
                             Shared.Abstractions
                                      ▲
                                      │
                            Shared.Infrastructure ◄──── tüm Modules.* implementasyonları

  Modüller arası (çapraz) oklar — SADECE başka modülün Contracts'ına:
    Modules.Ordering ──► Catalog.Contracts     (ICatalogModuleApi, StockReservationFailed)
    Modules.Catalog  ──► Ordering.Contracts    (OrderPlaced, OrderCancelled)
    Modules.Shipping ──► Ordering.Contracts    (OrderPlaced, OrderShipped, OrderCancelled)
```

Daha okunur liste hâlinde:

| Proje | Referans verdiği projeler |
|---|---|
| `Shared.Abstractions` | — (hiçbir şey) |
| `Shared.Infrastructure` | Shared.Abstractions (+ EF Core Sqlite, ASP.NET Core) |
| `Catalog.Contracts` / `Ordering.Contracts` / `Shipping.Contracts` | Shared.Abstractions |
| `Modules.Catalog` | Shared.Infrastructure, **Catalog.Contracts**, **Ordering.Contracts** (OrderPlaced/OrderCancelled'ı dinler) |
| `Modules.Ordering` | Shared.Infrastructure, **Ordering.Contracts**, **Catalog.Contracts** (ICatalogModuleApi + StockReservationFailed) |
| `Modules.Shipping` | Shared.Infrastructure, **Shipping.Contracts**, **Ordering.Contracts** |
| `ModularMonolith.Api` | Modules.Catalog, Modules.Ordering, Modules.Shipping |

**Neden bu yönde?**
- Bir modül başka bir modülün **implementasyonuna asla** referans vermez; sadece onun **Contracts** projesine.
  Contracts küçük, kararlı ve versiyonlanabilir bir "public API"dir. Implementasyon istediği kadar değişebilir.
- Contracts projeleri EF Core'a, ASP.NET'e, modül implementasyonlarına bağlı değildir → mikroservise geçişte
  aynen NuGet paketi olarak paylaşılabilir.
- Modül implementasyonlarını sadece Bootstrapper bilir (composition root). `internal` sayesinde Bootstrapper bile
  `CatalogModule` dışında hiçbir Catalog tipini göremez.
- Ordering ↔ Catalog arasında **iki yönlü** bir ilişki var ama döngüsel *proje* referansı yok:
  Ordering → Catalog.Contracts, Catalog → Ordering.Contracts. Contracts'lar implementasyonları bilmediği için döngü oluşmaz.

## Senkron vs asenkron iletişim

### Senkron — `ICatalogModuleApi` (in-process çağrı)

`src/Modules/Ordering/ModularMonolith.Modules.Ordering/Features/PlaceOrder/PlaceOrder.cs`:

```csharp
// Ordering, Catalog'un Product entity'sini GÖREMEZ; sadece sözleşmedeki ProductSnapshot'ı alır.
var products = (await catalog.GetProductsAsync(productIds, ct)).ToDictionary(p => p.Id);
```

Ne zaman? Cevaba **hemen** ihtiyaç varsa (fiyat olmadan sipariş oluşturulamaz). Bedeli: Ordering, Catalog'a
*zamansal* olarak bağlı (Catalog ayrı serviste olsaydı ve çökseydi sipariş alınamazdı).

### Asenkron — integration event + outbox

```csharp
db.Orders.Add(order);
db.AddIntegrationEvent(new OrderPlacedIntegrationEvent(...)); // OutboxMessages tablosuna INSERT
await db.SaveChangesAsync(ct);                                // ikisi TEK transaction
```

Sonra `OutboxProcessor<OrderingDbContext>` (≈500 ms polling) mesajı okur → `InMemoryEventBus` →
`ReserveStockOnOrderPlacedHandler` (Catalog) ve `CreateShipmentOnOrderPlacedHandler` (Shipping) **kendi scope'larında**
ve **kendi DbContext'leriyle** çalışır. Ordering bu iki handler'ın varlığından habersizdir.

Ne zaman? Yan etkiler (stok düş, kargo aç, e-posta at) için. Sonuç **eventually consistent**: sipariş 201 döndükten
kısa süre sonra stok düşer, kargo kaydı oluşur. Stok asenkron düşerken yetersiz kalırsa Catalog
`StockReservationFailedIntegrationEvent` yayınlar ve Ordering siparişi otomatik iptal eder (→ Shipping kargoyu iptal eder).

### Inbox (idempotency)

`IdempotentIntegrationEventHandler<TEvent, TDbContext>`:
1. `(EventId, HandlerName)` Inbox'ta varsa atla.
2. `HandleCoreAsync` → iş değişiklikleri (+ gerekirse yeni outbox event'leri).
3. Inbox kaydı ekle → **tek** `SaveChanges`.

Bir handler patlarsa outbox mesajı yeniden denenir; diğer (başarılı) handler'lar Inbox sayesinde ikinci kez çalışmaz.

## Bir isteğin yolculuğu: `POST /orders/{id}/cancel`

1. **`src/Bootstrapper/ModularMonolith.Api/Program.cs`** — `app.MapOrderingEndpoints()` route'u kaydetmişti.
2. **`src/Modules/Ordering/ModularMonolith.Modules.Ordering/OrderingModule.cs`** — `CancelOrder.Map(app)` ve `CancelOrder.Handler` DI kaydı.
3. **`.../Ordering/Features/CancelOrder/CancelOrder.cs`** — endpoint, `Handler.HandleAsync`'i çağırır;
   handler `OrderingDbContext`'ten siparişi yükler.
4. **`.../Ordering/Domain/Order.cs`** — `Order.Cancel(reason)` invariant'ları kontrol eder:
   `Shipped` → `Error.Conflict` (409), `Cancelled` → 409.
5. **`.../Ordering/Features/CancelOrder/CancelOrder.cs`** — başarılıysa `db.AddIntegrationEvent(new OrderCancelledIntegrationEvent(...))`
   + `SaveChangesAsync` → `Orders` güncellemesi ve `OutboxMessages` satırı **aynı transaction**. HTTP **204** döner.
   Hata varsa `src/Shared/ModularMonolith.Shared.Infrastructure/Http/ErrorExtensions.cs` → ProblemDetails (404/409).
6. **`src/Shared/ModularMonolith.Shared.Infrastructure/Outbox/OutboxProcessor.cs`** — `OutboxProcessor<OrderingDbContext>` satırı okur, deserialize eder.
7. **`src/Shared/ModularMonolith.Shared.Infrastructure/Messaging/InMemoryEventBus.cs`** — `IIntegrationEventHandler<OrderCancelledIntegrationEvent>` kayıtlarını bulur:
   - **`src/Modules/Catalog/.../Features/ReleaseStock/ReleaseStockOnOrderCancelledHandler.cs`** → rezervasyondaki stoğu iade eder (`catalog.db`).
   - **`src/Modules/Shipping/.../Features/CancelShipment/CancelShipmentOnOrderCancelledHandler.cs`** → kargoyu `Cancelled` yapar (`shipping.db`).
   - Her ikisi de **`.../Shared.Infrastructure/Inbox/IdempotentIntegrationEventHandler.cs`** üzerinden Inbox kaydı yazar.
8. Outbox satırı `ProcessedOnUtc` ile işaretlenir.

## Çalıştırma

```bash
cd samples/08-ModularMonolith
dotnet build
dotnet test                                    # 10 architecture + 4 integration testi
dotnet run --project src/Bootstrapper/ModularMonolith.Api   # http://localhost:5008
```

Veritabanları (`catalog.db`, `ordering.db`, `shipping.db`) çalışma dizininde `EnsureCreated()` ile oluşturulur
ve ürünler seed edilir. **Migration yok** (öğretim amaçlı sadelik); şemayı değiştirirseniz `.db` dosyalarını silin.

```bash
B=http://localhost:5008
curl -s $B/products

# Sipariş ver (2 klavye) → 201 {"id": "..."}
ID=$(curl -s -X POST $B/orders -H 'Content-Type: application/json' \
  -d '{"customerId":"aaaaaaaa-0000-0000-0000-000000000001","items":[{"productId":"11111111-1111-1111-1111-111111111111","quantity":2}]}' \
  | sed -E 's/.*"id":"([^"]+)".*/\1/')

sleep 1
curl -s $B/orders/$ID
curl -s $B/shipments/by-order/$ID        # Shipping, OrderPlaced'ı ASENKRON işledi → AwaitingShipment
curl -s $B/products                      # Catalog stoğu ASENKRON düştü → Klavye 8

curl -i -X POST $B/orders/$ID/ship       # 204
curl -i -X POST $B/orders/$ID/cancel     # 409 (kargolanmış sipariş iptal edilemez)

# Yeni sipariş + iptal → 204, ardından stok iade, kargo Cancelled
ID2=$(curl -s -X POST $B/orders -H 'Content-Type: application/json' \
  -d '{"customerId":"aaaaaaaa-0000-0000-0000-000000000001","items":[{"productId":"33333333-3333-3333-3333-333333333333","quantity":1}]}' \
  | sed -E 's/.*"id":"([^"]+)".*/\1/')
sleep 1
curl -i -X POST $B/orders/$ID2/cancel    # 204
sleep 1
curl -s $B/shipments/by-order/$ID2       # "status":"Cancelled"
```

Tüm istekler için: `src/Bootstrapper/ModularMonolith.Api/ModularMonolith.Api.http`.

## Bir modülü mikroservise çıkarmak için ne değişir? (ör. Catalog)

1. **Yeni host**: `Catalog.Api` adında yeni bir web projesi; `Program.cs` içinde `AddCatalogModule()` + `MapCatalogEndpoints()`.
   Modül kodu (Domain/Features) **olduğu gibi** taşınır.
2. **Senkron API**: Ordering tarafında `ICatalogModuleApi`'nin yeni bir implementasyonu yazılır
   (`HttpCatalogModuleApi` — HttpClient/gRPC + resilience). `PlaceOrder.cs` **değişmez**, sadece DI kaydı değişir.
3. **Event bus**: `InMemoryEventBus` yerine broker tabanlı `IEventBus` (ör. MassTransit + RabbitMQ). Outbox ve Inbox
   zaten var → mesaj kaybı/çift işleme problemleri zaten çözülmüş durumda.
4. **Veri**: `catalog` schema'sını ayrı veritabanına taşı (bu örnekte zaten ayrı dosya). Başka modül bu tablolara
   JOIN yapmadığı için kırılan sorgu olmaz.
5. **Contracts paylaşımı**: `Catalog.Contracts` ve `Ordering.Contracts` NuGet paketi (veya kopyalanmış sözleşme) olur;
   sözleşme versiyonlaması başlar.
6. **Operasyon**: ayrı CI/CD pipeline, ayrı deploy, gözlemlenebilirlik (distributed tracing), gateway route'u.
7. Monolitten `AddCatalogModule()` satırını sil. Bitti — sınırlar doğru çizildiyse diğer modüllerde kod değişmez.

## Deneyin

1. **Yeni modül, sıfır değişiklik**: `Notifications` modülü ekleyin; `ShipmentCreatedIntegrationEvent`'i dinleyip
   log yazsın. Shipping/Ordering'e dokunmadan çalıştığını görün. Architecture test'lere de ekleyin.
2. **Sınırı bilerek ihlal edin**: `Modules.Ordering.csproj`'a `Modules.Catalog` referansı ekleyip bir `internal` tipi
   kullanmaya çalışın (derleyici durdurur). Sonra Catalog'da bir sınıfı `public` yapın → `ModuleBoundaryTests` kırmızıya döner.
3. **Idempotency'yi kanıtlayın**: `ReserveStockOnOrderPlacedHandler`'ın sonuna geçici olarak
   `throw` ekleyin; Shipping kargo kaydını bir kez açar, Catalog her denemede tekrar dener — `InboxMessages` ve
   `OutboxMessages.Attempts` kolonlarını `sqlite3 catalog.db` ile inceleyin.
4. `ICatalogModuleApi` için `HttpCatalogModuleApi` yazın (aynı process'teki `/products` endpoint'ini çağırsın) ve
   sadece DI kaydını değiştirerek mikroservise geçişin 2. adımını prova edin.

## Bu örnekteki gözlemler

**Artılar**
- ✅ Tek `dotnet run` — debug, deploy ve test basit; modüller arası çağrılar in-process (ağ hatası yok).
- ✅ `internal` + architecture test'ler sınırları **mekanik** olarak koruyor; "disiplin" tek başına yetmiyor.
- ✅ Outbox + Inbox sayesinde asenkron akış güvenilir; mikroservise geçişte en zor kısım hazır.
- ✅ Her modül kendi iç stilini seçti: Catalog basit VSA/CRUD, Ordering zengin domain modeli.

**Eksiler**
- ❌ Eventual consistency: sipariş 201 döndükten hemen sonra `GET /shipments/by-order/{id}` 404 dönebilir (UI/test bunu bilmeli).
- ❌ Tüm modüller birlikte ölçeklenir ve birlikte deploy edilir.
- ❌ Ön kontrol (senkron stok sorgusu) ile asıl stok düşümü (asenkron) arasında yarış var; bu yüzden
  `StockReservationFailed → siparişi iptal et` telafi akışı gerekiyor (mini bir saga!).
- ❌ Bu örnekteki outbox processor tek instance içindir; çoklu instance'ta satır kilitleme (`FOR UPDATE SKIP LOCKED`) gerekir.
- ❌ Daha fazla proje ve "tesisat" kodu (Shared.Infrastructure) — küçük bir uygulama için ağır gelebilir.
