# 06 — CQRS (Seviye 2: aynı DB, ayrı yazma ve okuma modeli)

📖 İlgili doküman: [CQRS](../../01-uygulama-ici-mimariler/07-cqrs.md)

## Bu örnekte ne görüyorsunuz?

- **İki ayrı model**: `Write/` zengin `Order` aggregate'i + EF Core + normalize tablolar (`Orders`, `OrderLines`, `Products`); `Read/` Dapper + ham SQL + denormalize tablolar (`OrderSummaries`, `CustomerStats`).
- **Komutlar sadece ID/void döner** (`PlaceOrder` → ID, `ShipOrder`/`CancelOrder` → void). Veri okumak isteyen sorgu endpoint'ine gider.
- **Sorgular domain'e asla dokunmaz**: join yok, hesaplama yok; ekranın istediği satır hazır.
- **Projeksiyonlar**: yazma tarafındaki domain event'leri (`OrderPlaced/Shipped/Cancelled`) `System.Threading.Channels` + `BackgroundService` ile **asenkron** olarak okuma modeline yansıtılır.
- **Gözlemlenebilir eventual consistency**: projektörde kasıtlı `Projection:DelayMs = 500` gecikmesi → sipariş oluşturduktan hemen sonra `GET` 404 döner, ~1 sn sonra 200.
- **MediatR yok**: kendi `ICommandHandler<T>`, `ICommandHandler<T,TId>`, `IQueryHandler<T,R>` arayüzlerimiz + reflection ile DI kaydı. *CQRS ≠ MediatR.*

## CQRS seviyeleri — bu örnek nerede?

| Seviye | Yazma | Okuma | Senkronizasyon | Tutarlılık | |
|---|---|---|---|---|---|
| 1 | Domain + EF | Aynı tablolardan Dapper/projeksiyon | Gerek yok | Anında | |
| **2** | **Domain + EF (normalize)** | **Aynı DB'de ayrı denormalize tablolar** | **Event → projektör (in-process)** | **Eventual** | **◀ bu örnek** |
| 3 | Write DB | Ayrı Read DB (Elastic/Redis/Mongo) | Broker (RabbitMQ/Kafka) + Outbox | Eventual | |
| 4 | Event Store | Event'lerden projeksiyon | Event store subscription | Eventual | → [07-EventSourcing](../07-EventSourcing/) |

> Not: `GET /products` bilinçli olarak **Seviye 1** tarzındadır: ürün listesi için ayrı read model gereksiz, `Products` tablosu Dapper ile sadece okunur. CQRS bir spektrumdur; her sorgu için en ucuz yeterli seviyeyi seçin.

## Klasör yapısı

```
06-CQRS/
├── Cqrs.slnx
├── src/Cqrs.Api/
│   ├── Program.cs                          ← Write / Read / Projections bileşenlerini bağlar (port 5006)
│   ├── appsettings.json                    ← "Projection": { "DelayMs": 500 }
│   ├── Cqrs.Api.http
│   ├── Common/
│   │   ├── Handlers.cs                     ← ICommandHandler<T>, ICommandHandler<T,TId>, IQueryHandler<T,R>
│   │   ├── HandlerRegistration.cs          ← reflection ile handler kaydı
│   │   ├── Exceptions.cs                   ← NotFound / CommandValidation
│   │   └── ProblemExceptionHandler.cs      ← exception → ProblemDetails (400/404/409)
│   ├── Write/                              ← ★ YAZMA MODELİ
│   │   ├── Domain/
│   │   │   ├── Order.cs                    ← aggregate: Place/Ship/Cancel + domain event üretimi
│   │   │   ├── Product.cs
│   │   │   ├── Events.cs                   ← OrderPlaced, OrderShipped, OrderCancelled
│   │   │   └── DomainException.cs
│   │   ├── Commands/
│   │   │   ├── PlaceOrder.cs               ← PlaceOrderCommand + PlaceOrderHandler (→ Guid)
│   │   │   ├── ShipOrder.cs                ← (→ void)
│   │   │   └── CancelOrder.cs              ← (→ void)
│   │   ├── WriteDbContext.cs               ← normalize tablolar; SaveChanges sonrası event yayını
│   │   ├── IEventPublisher.cs              ← yazma tarafının dışarıya tek "kapısı"
│   │   └── Seed.cs
│   ├── Read/                               ← ★ OKUMA MODELİ
│   │   ├── ReadDb.cs                       ← Dapper bağlantısı + OrderSummaries/CustomerStats şeması
│   │   └── Queries/
│   │       ├── GetOrderSummary.cs
│   │       ├── ListOrdersByCustomer.cs
│   │       ├── GetCustomerStats.cs
│   │       └── ListProducts.cs
│   ├── Projections/                        ← ★ SENKRONİZASYON
│   │   ├── ProjectionChannel.cs            ← Channel<IDomainEvent> + ChannelEventPublisher
│   │   ├── ProjectionWorker.cs             ← BackgroundService: kanal → (gecikme) → projektörler
│   │   ├── OrderSummaryProjector.cs        ← event → OrderSummaries
│   │   ├── CustomerStatsProjector.cs       ← event → CustomerStats
│   │   └── ProjectionOptions.cs / IProjector.cs
│   └── Endpoints/
│       ├── CommandEndpoints.cs             ← POST /orders, /orders/{id}/ship, /orders/{id}/cancel
│       └── QueryEndpoints.cs               ← GET /orders/{id}, /customers/{id}/orders, /customers/{id}/stats, /products
└── tests/Cqrs.Tests/
    ├── CommandToQueryFlowTests.cs          ← komut → projeksiyon → sorgu (polling ile bekleyerek)
    └── OrderAggregateTests.cs              ← yazma modeli birim testleri
```

## Referans grafiği

Proje seviyesinde: `Cqrs.Tests ──► Cqrs.Api`. Tek proje içindeki klasör bağımlılıkları:

```
 Endpoints/CommandEndpoints ──► Write/Commands ──► Write/Domain
                                       │
                                       ▼
                               Write/WriteDbContext ──► IEventPublisher (Write'ta tanımlı arayüz)
                                                               ▲ implements
 Projections/ ─────────────────────────────────────────────────┘
     │  (Write/Domain/Events'i okur)
     └──► Read/ReadDb  ◄── Read/Queries ◄── Endpoints/QueryEndpoints

 Write ──✗──► Read        Read ──✗──► Write        Read/Queries ──✗──► Domain
```

**Neden bu yönde?** Yazma modeli ekranları bilmemeli; okuma modeli iş kurallarını bilmemeli. İkisini bağlayan tek şey **event'ler**dir ve köprüyü `Projections/` kurar. `Write`, `IEventPublisher` arayüzünü kendisi tanımlar ve implementasyonu `Projections/`'tan gelir (bağımlılığın tersine çevrilmesi) → yarın kanal yerine RabbitMQ'ya geçtiğinizde yazma tarafı değişmez (Seviye 3'e geçiş).

## Bir isteğin yolculuğu: `POST /orders/{id}/cancel`

**Senkron kısım (HTTP isteği):**
1. `Endpoints/CommandEndpoints.cs` → `ICommandHandler<CancelOrderCommand>`'ı DI'dan alır, `new CancelOrderCommand(id)` oluşturur.
2. `Write/Commands/CancelOrder.cs` → `CancelOrderHandler` siparişi `WriteDbContext` ile yükler (yoksa `NotFoundException` → 404).
3. `Write/Domain/Order.cs` → `Cancel()`: `Shipped`/`Cancelled` ise `DomainException` (→ `Common/ProblemExceptionHandler.cs` → **409**); değilse durum değişir ve `OrderCancelled` event'i eklenir.
4. `Write/WriteDbContext.cs` → `SaveChangesAsync`: `Orders` tablosu güncellenir, **ardından** event `IEventPublisher`'a verilir.
5. `Projections/ProjectionChannel.cs` → `ChannelEventPublisher` event'i kanala yazar. HTTP yanıtı **204** olarak döner — okuma modeli henüz eski!

**Asenkron kısım (arka plan):**

6. `Projections/ProjectionWorker.cs` → kanaldan event'i okur, `Projection:DelayMs` kadar bekler, yeni bir DI scope açar.
7. `Projections/OrderSummaryProjector.cs` → `UPDATE OrderSummaries SET Status='Cancelled'`.
8. `Projections/CustomerStatsProjector.cs` → `CustomerStats`'tan `OrderCount - 1`, `TotalSpent - Total`.
9. Artık `Read/Queries/GetOrderSummary.cs` (`GET /orders/{id}`) güncel `Cancelled` durumunu döner.

## Çalıştırma

```bash
cd samples/06-CQRS
dotnet build
dotnet test
dotnet run --project src/Cqrs.Api      # http://localhost:5006
```

> Öğretim amaçlı sadelik: **migration yok**. Başlangıçta yazma tabloları `EnsureCreated()`, okuma tabloları `CREATE TABLE IF NOT EXISTS` ile oluşturulur (`cqrs.db`).

### Eventual consistency'yi gözlemleyin

```bash
B=http://localhost:5006; C=aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa

# 1) Komut: sipariş oluştur → 201 {"id":"..."}
ID=$(curl -s -X POST $B/orders -H 'Content-Type: application/json' \
  -d '{"customerId":"'$C'","items":[{"productId":"11111111-1111-1111-1111-111111111111","quantity":2}]}' \
  | sed 's/.*"id":"\([^"]*\)".*/\1/')

# 2) HEMEN sorgula → 404 "Sipariş okuma modelinde yok ... (henüz projekte edilmemiş olabilir)"
curl -s -w ' → %{http_code}\n' $B/orders/$ID

# 3) 1 sn sonra → 200, status: Placed
sleep 1; curl -s -w ' → %{http_code}\n' $B/orders/$ID

# 4) Kargola → 204; hemen sorgula → hâlâ "Placed" (eski veri!); 1 sn sonra → "Shipped"
curl -s -o /dev/null -w 'ship → %{http_code}\n' -X POST $B/orders/$ID/ship
curl -s $B/orders/$ID; echo
sleep 1; curl -s $B/orders/$ID; echo

# 5) Kargolanmış siparişi iptal → 409
curl -s -w ' → %{http_code}\n' -X POST $B/orders/$ID/cancel

# 6) Okuma modelleri
curl -s $B/customers/$C/orders; echo
curl -s $B/customers/$C/stats;  echo
curl -s $B/products; echo
```

Gecikmeyi değiştirmek için: `dotnet run --project src/Cqrs.Api -- --Projection:DelayMs=3000`.

## ⚠ Bu örnekte bilinçli basitleştirmeler

- **Event kaybı riski**: `WriteDbContext.SaveChangesAsync` önce DB'ye yazar, sonra event'i kanala koyar. İkisi arasında process çökerse (veya uygulama kanalda bekleyen event'lerle kapanırsa) okuma modeli **kalıcı olarak** eskide kalır. Üretimde **Outbox deseni** gerekir: event'ler aynı transaction'da bir `Outbox` tablosuna yazılır, ayrı bir worker oradan okuyup yayınlar (bkz. [Outbox/Inbox](../../03-dagitik-desenler/02-outbox-inbox.md), [08-ModularMonolith](../08-ModularMonolith/)).
- **Read model yeniden oluşturma yok**: projektör hatasında event loglanıp geçiliyor. Event Sourcing'de bu sorun "baştan replay" ile çözülür (bkz. [07-EventSourcing](../07-EventSourcing/)).
- Tek projektör worker'ı → event sırası korunur. Birden fazla tüketicide sıra/idempotency ayrıca ele alınmalı.

## Deneyin

1. **Yeni okuma modeli**: "ürün bazlı satış adedi" (`ProductSales`: ProductName, SoldQuantity) tablosu ve projektörü ekleyin. Yazma tarafına **hiç dokunmadan** yapabildiğinizi görün. (İpucu: `OrderPlaced` event'ine kalem detayı eklemeniz gerekebilir — event tasarımının önemi!)
2. **Outbox**: `WriteDbContext`'te event'leri aynı transaction'da `OutboxMessages` tablosuna yazın; `ProjectionWorker`'ı kanal yerine bu tablodan okuyacak şekilde değiştirin. Uygulamayı projeksiyon sırasında öldürüp yeniden başlatın — event kaybolmuyor mu?
3. **Seviye 1'e geri dönün**: `GetOrderSummary`'yi `Orders` + `OrderLines` join'iyle (ayrı tablo olmadan) yazın; tutarlılık anında olur ama sorgu karmaşıklaşır. Hangisi ne zaman tercih edilir?

## Bu örnekteki gözlemler

**Artılar**
- ✅ `Order` aggregate'i ekran alanlarından (`ProductNames`, `ItemCount`, istatistikler) tamamen arınmış.
- ✅ Sorgular tek tablodan tek satır → basit, hızlı, cache'lenebilir.
- ✅ Yeni ekran = yeni projektör; yazma tarafı değişmiyor (`CustomerStats` bunun örneği).
- ✅ Kanal yerine broker koymak Seviye 3'e geçiş için yeterli; mimari hazır.

**Eksiler**
- ❌ Eventual consistency istemciye sızıyor: `POST` sonrası `GET` 404 dönebiliyor (UI'da optimistic update/polling gerekir).
- ❌ Daha fazla hareketli parça: event, kanal, worker, iki projektör, iki şema.
- ❌ Basit bir sipariş CRUD'u için açıkça fazla — CQRS'i tüm sisteme değil, ihtiyaç olan yere uygulayın.
