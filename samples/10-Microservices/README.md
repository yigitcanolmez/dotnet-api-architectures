# 10 — Mikroservisler + Event-Driven + Saga (Orchestration) + Outbox/Inbox + API Gateway + Resilience

İlgili dokümanlar:
[Mikroservisler](../../02-dagitik-mimariler/02-mikroservisler.md) ·
[Event-Driven Architecture](../../02-dagitik-mimariler/03-event-driven-architecture.md) ·
[Saga](../../03-dagitik-desenler/01-saga.md) ·
[Outbox & Inbox](../../03-dagitik-desenler/02-outbox-inbox.md) ·
[API Gateway & BFF](../../03-dagitik-desenler/03-api-gateway-bff.md) ·
[Dayanıklılık Desenleri](../../03-dagitik-desenler/05-dayaniklilik-desenleri.md)

## Bu örnekte ne görüyorsunuz

- **Database per service**: Ordering, Inventory ve Payment ayrı süreçler, her birinin **kendi SQLite dosyası** var (`ordering.db`, `inventory.db`, `payment.db`). Hiçbir servis diğerinin tablosuna dokunmuyor.
- **Saga Orchestration**: Sipariş akışı Ordering içindeki **MassTransit state machine** (`OrderStateMachine`) tarafından yönetilir; adımlar **komut** (Send) olarak gönderilir, sonuçlar **event** (Publish) olarak döner. Ödeme reddedilirse **compensation** (`ReleaseStock`) çalışır.
- **Transactional Outbox**: `POST /orders`, `Order` kaydını ve `OrderSubmitted` mesajını **aynı SQLite transaction'ında** yazar (MassTransit EF Core outbox). RabbitMQ kapalıyken bile sipariş alınır, broker gelince mesaj gider (aşağıda gerçek deneme var).
- **Inbox / Idempotent consumer**: Tüm consumer endpoint'leri `UseEntityFrameworkOutbox<TDbContext>` ile sarılı → aynı `MessageId` iki kez işlenmez; ayrıca `OrderId` üzerindeki unique index'ler doğal idempotency sağlar.
- **API Gateway (YARP)**: Tek giriş noktası, rate limiting (fixed window), istek loglama + correlation id, aktif health check ve **API Composition** (`GET /api/order-details/{id}` üç servisi **paralel** çağırır).
- **Resilience**: MassTransit `UseMessageRetry`, gateway HttpClient'larında `AddStandardResilienceHandler()` (timeout + retry + circuit breaker), servis düşerse **kısmi yanıt** (graceful degradation).

## Sistem diyagramı

```
                         ┌──────────────────────────────────────────────┐
  curl / SPA ──HTTP────► │  Gateway.Api  :5100  (YARP)                  │
                         │  /api/orders/**       → ordering             │
                         │  /api/products/**     → inventory            │
                         │  /api/reservations/** → inventory            │
                         │  /api/payments/**     → payment              │
                         │  /api/order-details/{id}  (API Composition)  │
                         │  rate limit · request log · active health    │
                         └─────┬──────────────────┬──────────────┬──────┘
                               │ HTTP             │ HTTP         │ HTTP
                 ┌─────────────▼─────┐  ┌─────────▼───────┐  ┌───▼─────────────┐
                 │ Ordering.Api :5101│  │Inventory.Api    │  │ Payment.Api     │
                 │ Order + SAGA      │  │ :5102           │  │ :5103           │
                 │ (orkestratör)     │  │ stok/rezervasyon│  │ tahsilat        │
                 └───┬───────────▲───┘  └──▲──────────┬───┘  └──▲──────────┬───┘
                     │ordering.db│         │inventory.db│        │payment.db │
                     │           │         │            │        │           │
          Send(cmd)/Publish(evt) │  cmd    │     evt    │  cmd   │    evt    │
                     ▼           │         │            ▼        │           ▼
               ┌──────────────────────────────────────────────────────────────────┐
               │                   RabbitMQ  (:5672, UI :15672)                    │
               │ kuyruklar: order-state · reserve-stock · release-stock ·          │
               │            process-payment · order-confirmed · order-rejected ·   │
               │            order-cancelled                                        │
               └──────────────────────────────────────────────────────────────────┘
```

## Servisler, sorumlulukları ve veritabanları

| Servis | Port | Sorumluluk | DB | Tablolar |
|---|---|---|---|---|
| **Gateway.Api** | 5100 | Yönlendirme, rate limit, log, health, composition. İş kuralı **yok**. | — | — |
| **Ordering.Api** | 5101 | Siparişi almak, sipariş saga'sını **orkestre** etmek, sipariş durumunu tutmak. | `ordering.db` | `Orders`, `OrderLines`, `CatalogItems` (fiyat kopyası), `OrderSagas`, `InboxState`, `OutboxState`, `OutboxMessage` |
| **Inventory.Api** | 5102 | Stok ve rezervasyonlar. Saga'nın **compensatable** adımı. | `inventory.db` | `Products`, `Reservations`, `ReservationItems`, `InboxState`, `OutboxState`, `OutboxMessage` |
| **Payment.Api** | 5103 | Tahsilat (kural: tutar > 10.000 → red). Saga'nın **pivot** adımı. | `payment.db` | `Payments`, `InboxState`, `OutboxState`, `OutboxMessage` |

Seed: 3 ortak ürün (Klavye 750 / stok 10, Mouse 350 / 20, Monitör 6500 / 5). Ordering'deki `CatalogItems` bu ürünlerin **fiyat/isim kopyasıdır** (bkz. "Distributed monolith" bölümü).

## Klasör ağacı

```
10-Microservices/
├── Microservices.slnx
├── docker-compose.yml              # RabbitMQ + 4 servis (tam sistem)
├── docker-compose.infra.yml        # Sadece RabbitMQ (lokal dotnet run için)
├── run-local.sh / stop-local.sh    # RabbitMQ + 4 servisi arka planda başlat/durdur
├── src/
│   ├── Shared/Contracts/           # SADECE mesaj sözleşmeleri (record'lar), hiçbir bağımlılık yok
│   │   ├── Commands/Commands.cs    # ReserveStock, ReleaseStock, ProcessPayment
│   │   ├── Commands/QueueNames.cs  # Komut kuyruk adları (adresleme de sözleşmenin parçası)
│   │   └── Events/Events.cs        # OrderSubmitted, StockReserved, StockReservationFailed, PaymentCompleted,
│   │                               # PaymentFailed, OrderConfirmed, OrderRejected, OrderCancelled
│   ├── Services/
│   │   ├── Ordering/Ordering.Api/
│   │   │   ├── Program.cs                      # MassTransit: outbox, saga repo, retry, RabbitMQ
│   │   │   ├── Domain/Order.cs                 # Order aggregate + iş kuralları
│   │   │   ├── Domain/CatalogItem.cs           # Yerel fiyat kopyası (okuma modeli)
│   │   │   ├── Infrastructure/OrderingDbContext.cs  # İş verisi + saga + outbox aynı DbContext'te
│   │   │   ├── Saga/OrderStateMachine.cs       # ORKESTRATÖR: akışın tek, okunabilir tanımı
│   │   │   ├── Saga/OrderState.cs, OrderStateMap.cs # Saga durumu ve EF eşlemesi
│   │   │   ├── Consumers/OrderOutcomeConsumers.cs   # OrderConfirmed/OrderRejected → Order tablosu
│   │   │   ├── Endpoints/OrderEndpoints.cs     # POST /orders (outbox), GET, ship, cancel, saga
│   │   │   ├── Dockerfile · Ordering.Api.http
│   │   ├── Inventory/Inventory.Api/
│   │   │   ├── Domain/Product.cs, Reservation.cs
│   │   │   ├── Infrastructure/InventoryDbContext.cs, StockReleaser.cs
│   │   │   ├── Consumers/StockConsumers.cs     # ReserveStock, ReleaseStock (compensation), OrderCancelled
│   │   │   ├── Endpoints/InventoryEndpoints.cs # GET /products, /reservations
│   │   │   ├── Dockerfile · Inventory.Api.http
│   │   └── Payment/Payment.Api/
│   │       ├── Domain/Payment.cs               # 10.000 limit kuralı
│   │       ├── Consumers/ProcessPaymentConsumer.cs
│   │       ├── Endpoints/PaymentEndpoints.cs   # GET /payments
│   │       ├── Dockerfile · Payment.Api.http
│   └── Gateway/Gateway.Api/
│       ├── Program.cs                          # YARP + rate limiter + resilience'lı HttpClient'lar
│       ├── appsettings.json                    # ReverseProxy rotaları, cluster'lar, health check
│       ├── Composition/OrderDetailsEndpoint.cs # GET /api/order-details/{id} (paralel birleştirme)
│       ├── Composition/DownstreamClients.cs    # Typed HttpClient'lar + fallback
│       ├── Middleware/RequestLoggingMiddleware.cs
│       └── Dockerfile · Gateway.Api.http
└── tests/Ordering.Tests/OrderStateMachineTests.cs  # Saga, MassTransit in-memory test harness ile
```

## Proje referans grafiği

```
 Ordering.Api ──┐
 Inventory.Api ─┼──► Contracts   (sadece record'lar; MassTransit'e bile bağlı değil)
 Payment.Api ───┘

 Gateway.Api   ──► (hiçbir proje)        Ordering.Tests ──► Ordering.Api
```

**Neden bu yönde?** Servisler birbirine **asla** proje referansı vermez; aralarındaki tek ortak şey **mesaj sözleşmesidir**. Gateway mesajlaşmaya katılmadığı için Contracts'ı da bilmez; servislerle sadece HTTP/JSON konuşur. Böylece her servis ayrı derlenir, ayrı imaj olur, ayrı deploy edilir.

> ⚠️ **Paylaşılan Contracts paketi SADECE mesaj sözleşmesi içermeli, domain DEĞİL.** İçine `Order` entity'si, iş kuralı, yardımcı servis, EF tipi, "ortak base class" koyduğunuz anda tüm servisler aynı paketin her sürümüne kilitlenir ve [distributed monolith](../../02-dagitik-mimariler/02-mikroservisler.md#distributed-monolith-belirtileri-) belirtisi başlar. Gerçek projede bu paket sürümlenir (NuGet) ya da her servis sözleşmeyi kendine **kopyalar**; MassTransit mesajları tip adıyla (namespace + ad) eşlediği için ikisi de çalışır. Sözleşmeye alan **eklemek** güvenlidir, silmek/yeniden adlandırmak kırıcıdır.

## Saga state machine (OrderStateMachine)

```
                       OrderSubmitted
                 ┌──────────────────────────┐
   (Initial) ────┘  Send ReserveStock        ▼
                                     ┌───────────────┐
                                     │ AwaitingStock │
                                     └──┬─────────┬──┘
                   StockReserved        │         │   StockReservationFailed
                   Send ProcessPayment  │         │   Publish OrderRejected
                                        ▼         │   (telafi edilecek adım yok)
                              ┌─────────────────┐ │
                              │ AwaitingPayment │ │
                              └──┬───────────┬──┘ │
             PaymentCompleted    │           │    │
             Publish OrderConfirmed          │ PaymentFailed
                                 ▼           │ Send ReleaseStock  ◄── COMPENSATION
                          ┌───────────┐      │ Publish OrderRejected
                          │ Completed │      ▼    ▼
                          └───────────┘   ┌──────────┐
                                          │  Failed  │
                                          └──────────┘
   Tekrar gelen/geciken mesajlar (at-least-once) ilgili durumlarda Ignore(...) ile yutulur.
```

Saga satırı `OrderSagas` tablosunda (`ordering.db`) saklanır; `GET /orders/{id}/saga` ile anlık durumu görebilirsiniz. **Optimistic concurrency**: `OrderState.Version` concurrency token'dır ve `OrderingDbContext.SaveChangesAsync` her güncellemede artırır; aynı saga'ya eşzamanlı iki mesaj gelirse biri `DbUpdateConcurrencyException` alır ve retry ile taze veriyle tekrar işlenir.

## Mesaj akışı — sekans diyagramları

**Mutlu yol** (1 klavye, 750 TRY):

```
Client   Gateway   Ordering(API)  ordering.db  Outbox→RabbitMQ   Saga(Ordering)   Inventory   Payment
  │ POST /api/orders │                │              │                 │               │          │
  │────────►│───────►│ INSERT Order + OutboxMessage (TEK TRANSACTION)    │               │          │
  │◄── 201 {id} ─────│ (status: Pending)              │                 │               │          │
  │         │        │                │  OrderSubmitted (publish) ─────►│               │          │
  │         │        │                │              │  Send ReserveStock ────────────►│          │
  │         │        │                │              │                 │   rezerve et  │          │
  │         │        │                │              │◄────────────── StockReserved ───│          │
  │         │        │                │              │  Send ProcessPayment ──────────────────────►│
  │         │        │                │              │◄─────────────────────────── PaymentCompleted│
  │         │        │                │              │  Publish OrderConfirmed         │          │
  │         │        │◄── OrderConfirmedConsumer: Order.Status = Confirmed             │          │
```

**Compensation** (2 monitör = 13.000 TRY > 10.000):

```
Saga                         Inventory                         Payment
 │ Send ReserveStock ───────►│ Monitör 5→3 (Reserved)            │
 │◄──────── StockReserved ───│                                   │
 │ Send ProcessPayment ─────────────────────────────────────────►│ Payment(Failed) kaydı
 │◄──────────────────────────────────────────────── PaymentFailed│
 │ Send ReleaseStock ───────►│ Monitör 3→5, Reservation=Released  ◄── C2: T2'nin semantik tersi
 │ Publish OrderRejected ──► Ordering: Order.Status = Rejected
```

**Stok yok** (100 klavye): `ReserveStock` → Inventory `Reservation(Rejected)` + `StockReservationFailed` → Saga `OrderRejected` yayınlar. Ödeme hiç denenmez, telafi edilecek adım yoktur.

## Bir isteğin yolculuğu

### `POST /api/orders` (ana akış)

1. `src/Gateway/Gateway.Api/Middleware/RequestLoggingMiddleware.cs` — correlation id ekler, isteği loglar.
2. `src/Gateway/Gateway.Api/appsettings.json` → `ReverseProxy:Routes:orders` — rate limiter (`fixed`) uygulanır, `PathRemovePrefix: /api` ile `/orders` olarak `ordering` cluster'ına (`:5101`) iletilir.
3. `src/Services/Ordering/Ordering.Api/Endpoints/OrderEndpoints.cs` → `PlaceOrder` — doğrulama, fiyatlar yerel `CatalogItems`'tan, `Domain/Order.cs` → `Order.Place(...)` (invariant'lar).
4. Aynı metotta `IPublishEndpoint.Publish(OrderSubmitted)` — `Program.cs`'teki `UseBusOutbox()` sayesinde broker'a değil **`OutboxMessage` tablosuna** gider; `SaveChangesAsync` Order + mesajı tek transaction'da commit eder → `201 Created`.
5. MassTransit'in arka plan teslimat servisi outbox'ı okuyup RabbitMQ'ya yayınlar.
6. `src/Services/Ordering/Ordering.Api/Saga/OrderStateMachine.cs` — `Initially(When(OrderSubmitted))`: saga satırı oluşur, `ReserveStock` `queue:reserve-stock`'a gönderilir → `AwaitingStock`.
7. `src/Services/Inventory/Inventory.Api/Consumers/StockConsumers.cs` → `ReserveStockConsumer` — inbox kontrolü (endpoint filtresi), stok düşer, `StockReserved` (consumer outbox'ı ile atomik) yayınlanır.
8. Saga: `StockReserved` → `ProcessPayment` gönderir → `AwaitingPayment`.
9. `src/Services/Payment/Payment.Api/Consumers/ProcessPaymentConsumer.cs` — `Domain/Payment.cs` kuralı; `PaymentCompleted` veya `PaymentFailed`.
10. Saga: `PaymentCompleted` → `OrderConfirmed` yayınlar (`Completed`) / `PaymentFailed` → `ReleaseStock` + `OrderRejected` (`Failed`).
11. `src/Services/Ordering/Ordering.Api/Consumers/OrderOutcomeConsumers.cs` — `Order.Confirm()` / `Order.Reject()`; `GET /api/orders/{id}` artık `Confirmed`/`Rejected` döner.

### `POST /api/orders/{id}/cancel`

1. Gateway → `orders` rotası → Ordering `/orders/{id}/cancel`.
2. `Endpoints/OrderEndpoints.cs` → `CancelOrder` → `Domain/Order.cs` → `Order.Cancel()`:
   `Shipped`/`Cancelled`/`Rejected` → **409**; `Pending` (saga sürüyor — *semantic lock*) → **409**; `Confirmed` → `Cancelled`.
3. Aynı outbox transaction'ında `OrderCancelled` event'i yayınlanır → **204**.
4. `src/Services/Inventory/Inventory.Api/Consumers/StockConsumers.cs` → `OrderCancelledConsumer` → `Infrastructure/StockReleaser.cs` rezervasyonu `Released` yapar, stok geri döner. (Ödeme iadesi bu örnekte yok → "Deneyin".)

## Outbox burada hangi problemi çözüyor? (Dual write)

`POST /orders` iki şey yapmak zorunda: **siparişi DB'ye yaz** ve **OrderSubmitted yayınla**. Bunlar iki ayrı sistem (SQLite + RabbitMQ); 2PC yok.

- Önce DB sonra publish: arada süreç çökerse sipariş `Pending`'de sonsuza kadar kalır, saga hiç başlamaz.
- Önce publish sonra DB: DB yazımı başarısızsa ortada "hayalet" sipariş için stok ayrılır.

Çözüm: `AddEntityFrameworkOutbox<OrderingDbContext>(o => { o.UseSqlite(); o.UseBusOutbox(); })`. Publish edilen mesaj `OutboxMessage` tablosuna, `Order` ile **aynı** `SaveChangesAsync` (aynı SQLite transaction) içinde yazılır; `BusOutboxDeliveryService` sonra broker'a taşır. Garanti: *sipariş kaydedildiyse mesaj eninde sonunda yayınlanır* (at-least-once).

Aynı mekanizma consumer tarafında da var: Inventory/Payment consumer'larının içindeki `context.Publish(...)` çağrıları, iş verisi ve inbox kaydıyla birlikte commit edilir (`UseEntityFrameworkOutbox<TDbContext>(context)`), saga'nın `Send`/`Publish`'leri de saga satırıyla birlikte.

**SQLite notu:** MassTransit 8.5.5'te SQLite için yerleşik bir lock statement provider var (`o.UseSqlite()` hem outbox'ta hem saga repository'de: `r.UseSqlite()`), ek bir şey gerekmedi. SQLite satır kilidi desteklemediği için yazmalar dosya seviyesinde serileşir; `Microsoft.Data.Sqlite` transaction'ları `BEGIN IMMEDIATE` ile açar ve meşgul durumda bekler. Okuyucuların yazıcıyı bloklamaması için açılışta `PRAGMA journal_mode=WAL` çalıştırılıyor. Üretimde Postgres/SQL Server kullanın (`UsePostgres()` / `UseSqlServer()`).

**Gerçek deneme (docker compose ile yapıldı):** RabbitMQ durduruldu → `POST /api/orders` yine **201** döndü, sipariş `Pending` kaldı → RabbitMQ yeniden başlatıldı → ~16 sn içinde sipariş **Confirmed** oldu. Mesaj kaybolmadı, çünkü outbox tablosunda bekliyordu.

```bash
docker compose stop rabbitmq
curl -s -X POST localhost:5100/api/orders -H 'Content-Type: application/json' \
  -d '{"customerId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","items":[{"productId":"22222222-2222-2222-2222-222222222222","quantity":2}]}'
docker compose start rabbitmq    # birkaç saniye sonra GET ile Confirmed görün
```

## Inbox / Idempotency

RabbitMQ **at-least-once** teslim eder; aynı `ReserveStock` iki kez gelebilir (ör. consumer işledi ama ack göndermeden çöktü). Bu örnekte iki katman var:

1. **MassTransit EF Inbox** — `AddConfigureEndpointsCallback(... cfg.UseEntityFrameworkOutbox<TDbContext>(context))`: her mesajın `MessageId`'si `InboxState` tablosuna, iş verisiyle **aynı transaction'da** yazılır. Aynı `MessageId` `DuplicateDetectionWindow` (30 dk) içinde tekrar gelirse consumer **hiç çağrılmaz**. Ayrı bir `InboxMessages` tablosu yazmaya gerek kalmadı.
2. **Doğal idempotency** — `Reservations.OrderId` ve `Payments.OrderId` **unique**. Farklı `MessageId` ile aynı sipariş için tekrar komut gelirse (ör. saga'nın yeniden gönderimi) yeni rezervasyon/tahsilat yapılmaz, **önceki sonuç yeniden yayınlanır**. `Order.Confirm()/Reject()` ve `Reservation.Release()` de tekrar çağrılmaya dayanıklı. Saga tarafında ise geç/tekrar gelen event'ler `Ignore(...)` ile yutulur.

> Outbox + Inbox = **effectively-once processing** (exactly-once delivery değil!).

## Gateway ve API Composition

- **Routing (YARP)**: `appsettings.json` → `ReverseProxy`. `/api` öneki `PathRemovePrefix` ile atılır; iç servisler dışarıya kendi URL yapılarını açmak zorunda kalmaz.
- **Rate limiting**: `AddFixedWindowLimiter("fixed")` — 10 saniyede 100 istek (konfigüre edilebilir: `RateLimiting:PermitLimit`). Denendi: 110 hızlı istekte 100×200, 10×**429**.
- **Request logging**: method, path, status, süre ve `X-Correlation-Id` (yoksa üretilir, YARP iç servislere iletir).
- **Active health check**: Her cluster 10 sn'de bir `/health` yoklanır; düşen hedef `Unhealthy` işaretlenir (log: `Active health state of destination d1 ... Unhealthy`). Servislerin `/health`'i MassTransit bus sağlığını da içerir (`AddHealthChecks()` + MassTransit'in otomatik kaydı).
- **API Composition**: `GET /api/order-details/{id}` → Ordering (`/orders/{id}` ve `/orders/{id}/saga`), Payment (`/payments?orderId=`) ve Inventory (`/reservations?orderId=`) **`Task.WhenAll` ile paralel** çağrılır ve tek JSON'da birleştirilir. Gateway'de iş kuralı yoktur; sadece birleştirme yapar ("aptal gateway").

## Resilience

| Nerede | Ne | Dosya |
|---|---|---|
| Tüm consumer/saga endpoint'leri | `UseMessageRetry(r => r.Intervals(100, 500, 1000, 2000, 5000))` — geçici DB kilidi, concurrency çakışması | her servisin `Program.cs` |
| RabbitMQ bağlantısı | MassTransit broker'a ulaşamazsa otomatik yeniden bağlanır (compose'da açılışta görülür) | — |
| Gateway → servis HTTP | `AddStandardResilienceHandler`: attempt timeout 2 sn, toplam 8 sn, 2 retry (exponential + jitter), circuit breaker (%50, 30 sn pencere, 15 sn açık) | `Gateway.Api/Program.cs` |
| Gateway composition | **Fallback / graceful degradation**: Payment kapalıyken `order-details` yine 200 döner, `payments: null`, `warnings: ["Payment erişilemez: HttpRequestException"]` (denendi) | `Composition/DownstreamClients.cs` |
| Gateway girişi | Rate limiting (429) | `Gateway.Api/Program.cs` |
| Mimari | En iyi dayanıklılık: sipariş akışı **senkron zincir değil**, mesajla. Payment kapalıyken bile sipariş alınır; kuyrukta bekler. | — |

**Ödeme zaman aşımı (Schedule) neden yok?** Dokümandaki `Schedule(() => PaymentTimeout, ...)` bir **mesaj zamanlayıcısı** gerektirir. RabbitMQ'da `AddDelayedMessageScheduler()` + `cfg.UseDelayedMessageScheduler()` kullanımı `rabbitmq_delayed_message_exchange` **plugin'ini** ister; standart `rabbitmq:3-management` imajında yoktur. MassTransit 8.5.5'te eski `UseInMemoryScheduler` da artık yok (Quartz tabanlı ayrı paket gerekir). Seçenekler:
1. Plugin'li RabbitMQ imajı (Dockerfile'da `rabbitmq-plugins enable rabbitmq_delayed_message_exchange`) + `AddDelayedMessageScheduler()`.
2. Quartz.NET / Hangfire ile `AddQuartzConsumers()` + `UsePublishMessageScheduler()`.
3. Azure Service Bus / Amazon SQS gibi native zamanlamalı transport.
Sadeliği korumak için bu örnek timeout'u atlıyor (bkz. "Deneyin").

## Distributed monolith'e düşmemek için bu örnekte neye dikkat edildi

- **Paylaşılan DB yok**: Her servisin kendi `.db` dosyası, kendi `DbContext`'i. Compose'da her biri ayrı volume.
- **Paylaşılan domain yok**: `Contracts` sadece serileştirilebilir record'lar içerir; MassTransit'e bile bağımlı değil. `Order`, `Product`, `Payment` sınıfları sadece kendi servislerinde.
- **Paylaşılan "Common/Infrastructure" kütüphanesi yok**: MassTransit kurulumu her `Program.cs`'te bilinçli olarak tekrarlandı. Az kod tekrarı, her sürümde tüm servisleri birlikte güncelleme zorunluluğundan ucuzdur.
- **Senkron çağrı zinciri yok**: Sipariş alırken Ordering, Inventory'ye HTTP ile fiyat/stok sormaz; fiyatlar kendi `CatalogItems` kopyasından gelir (gerçekte `ProductPriceChanged` gibi event'lerle güncellenir), stok kontrolü saga'da asenkron yapılır. Senkron HTTP sadece **okuma amaçlı** composition'da ve dayanıklılık önlemleriyle var.
- **Katılımcılar birbirini tanımaz**: Inventory, Payment'tan; Payment, Inventory'den habersiz. Akış bilgisi tek yerde (saga). Komutlar mantıksal kuyruk adına gider (`queue:reserve-stock`), servis adresine değil.
- **Bağımsız deploy**: Her servisin kendi Dockerfile'ı ve imajı var; gateway Contracts'ı hiç referans etmez.
- **Sözleşme evrimi**: Event'lere alan eklemek geriye uyumludur; mevcut alanları değiştirmek yerine yeni sürüm (`OrderSubmittedV2`) tercih edilmeli.

## Çalıştırma

### 1) En kolay yol: script

```bash
cd samples/10-Microservices
./run-local.sh      # RabbitMQ (docker compose -f docker-compose.infra.yml) + 4 servis (dotnet run, arka planda)
# ... deneyin ...
./stop-local.sh     # servisleri PID ile durdurur, RabbitMQ container'ını kaldırır
```
Loglar: `.local/logs/*.log`. RabbitMQ yönetim paneli: http://localhost:15672 (guest/guest) — kuyrukları görün.

### 2) Elle

```bash
docker compose -f docker-compose.infra.yml up -d            # sadece RabbitMQ
dotnet run --project src/Services/Ordering/Ordering.Api      # ayrı terminallerde
dotnet run --project src/Services/Inventory/Inventory.Api
dotnet run --project src/Services/Payment/Payment.Api
dotnet run --project src/Gateway/Gateway.Api
```
(DB dosyaları çalıştırdığınız dizinde oluşur. Migration yok: uygulama açılırken `EnsureCreated()` + seed — öğretim amaçlı sadelik.)

### 3) Tam compose (RabbitMQ + 4 servis container'da)

```bash
docker compose up --build          # Gateway: http://localhost:5100
docker compose down -v
```
Dockerfile'lar multi-stage (`sdk:10.0` → `aspnet:10.0`), root olmayan kullanıcıyla çalışır. Ortak `samples/Directory.Build.props` build context dışında kaldığı için compose `additional_contexts: samplesroot: ..` kullanır. Tek imaj için:
`docker build --build-context samplesroot=.. -f src/Services/Ordering/Ordering.Api/Dockerfile -t microservices-ordering .`
Servisler compose'da birbirine servis adıyla bağlanır (`RabbitMq__Host=rabbitmq`, `ReverseProxy__Clusters__ordering__Destinations__d1__Address=http://ordering:5101` ...).

### Test

```bash
dotnet test     # OrderStateMachine: mutlu yol, payment fail → ReleaseStock, stock fail, tekrar mesaj
```
Testler RabbitMQ gerektirmez: `AddMassTransitTestHarness` (in-memory transport + in-memory saga repository).

### curl ile uçtan uca

```bash
G=http://localhost:5100; C=aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
curl -s $G/api/products

# (a) 1 klavye → birkaç saniye içinde Confirmed
A=$(curl -s -X POST $G/api/orders -H 'Content-Type: application/json' \
  -d "{\"customerId\":\"$C\",\"items\":[{\"productId\":\"11111111-1111-1111-1111-111111111111\",\"quantity\":1}]}" | jq -r .id)
# (b) 2 monitör (13000) → ödeme reddi → Rejected + rezervasyon Released (compensation)
B=$(curl -s -X POST $G/api/orders -H 'Content-Type: application/json' \
  -d "{\"customerId\":\"$C\",\"items\":[{\"productId\":\"33333333-3333-3333-3333-333333333333\",\"quantity\":2}]}" | jq -r .id)
# (c) 100 klavye → stok yok → Rejected
D=$(curl -s -X POST $G/api/orders -H 'Content-Type: application/json' \
  -d "{\"customerId\":\"$C\",\"items\":[{\"productId\":\"11111111-1111-1111-1111-111111111111\",\"quantity\":100}]}" | jq -r .id)
sleep 3
curl -s $G/api/orders/$A            # status: Confirmed
curl -s $G/api/orders/$A/saga       # currentState: Completed
curl -s "$G/api/reservations?orderId=$B"   # status: Released
curl -s $G/api/order-details/$B     # (d) Ordering + Saga + Payment + Inventory tek yanıtta
curl -s -X POST $G/api/orders/$A/ship -i     # 204
curl -s -X POST $G/api/orders/$A/cancel -i   # 409 (kargolanmış)
```

Doğrulanmış çıktı özeti (lokal `dotnet run` ve `docker compose` ile ayrı ayrı):

| Senaryo | Order | Saga | Payment | Reservation |
|---|---|---|---|---|
| (a) 1 klavye | Confirmed | Completed | Completed | Reserved |
| (b) 2 monitör | Rejected | Failed | Failed | **Released** (stok 5'e döndü) |
| (c) 100 klavye | Rejected | Failed | — | Rejected |

## Deneyin

1. **Ödeme zaman aşımı**: RabbitMQ'ya `rabbitmq_delayed_message_exchange` plugin'ini ekleyen bir imaj yapın, `x.AddDelayedMessageScheduler()` + `cfg.UseDelayedMessageScheduler()` ile `Schedule<OrderState, PaymentTimeout>` ekleyin; Payment consumer'ına `await Task.Delay(...)` koyup saga'nın `ReleaseStock` ile telafi ettiğini görün.
2. **İade (refund)**: Onaylı sipariş iptal edilince Payment'ın da `OrderCancelled`'ı dinleyip `Refunded` kaydı oluşturmasını sağlayın. Ordering'e hiç dokunmanız gerekmediğine dikkat edin (event-driven'ın gücü).
3. **Idempotency'yi gözlemleyin**: RabbitMQ panelinden (`reserve-stock` kuyruğuna "Publish message") aynı `messageId` ile mesajı iki kez gönderin; stok bir kez düşmeli. Sonra `UseEntityFrameworkOutbox` satırını kaldırıp farkı görün (unique index yine korur).
4. **Resilience**: Inventory'yi durdurun, `GET /api/order-details/{id}` çağırın; `warnings` dolu ama yanıt 200. 5+ başarısız çağrıdan sonra circuit breaker'ın açıldığını gateway loglarında görün.

## Bu örnekteki artı / eksi gözlemleri

**Artılar**
- Akış `OrderStateMachine.cs`'te tek bakışta okunuyor; "sipariş şu an hangi adımda?" sorusunun cevabı bir SELECT.
- Broker veya Payment kapalıyken bile sipariş alınabiliyor (temporal decoupling + outbox).
- Her servis küçük ve kendi verisinin tek sahibi; Inventory veya Payment, Ordering'e dokunmadan değişebilir.

**Eksiler**
- Basit bir "sipariş ver" için 4 süreç, 1 broker, 7 kuyruk, 3 DB ve ~15 dosya — aynı iş modüler monolitte tek transaction'dı.
- Eventual consistency: `POST` sonrası sipariş birkaç saniye `Pending`; istemci polling yapmalı (veya SignalR/webhook).
- Hata ayıklama dağıtık: loglar 4 sürece yayılmış; gerçek projede OpenTelemetry + merkezi log şart.
- Saga izolasyonu yok: rezerve edilmiş ama ödenmemiş stok diğer siparişlere "yok" görünür (dirty read benzeri) — `Pending` durumu *semantic lock* görevi görür.

## Bilinen kısıtlar

- Migration yok; `EnsureCreated()` kullanılıyor. Şema değişirse `*.db` dosyalarını silin.
- SQLite tek yazıcılıdır; yük altında üretim için uygun değildir (outbox/saga için Postgres/SQL Server önerilir).
- Ödeme timeout'u (scheduler) ve iade (refund) bilinçli olarak dışarıda bırakıldı (yukarıdaki açıklamalar).
- Kimlik doğrulama (JWT) gateway'de yok; dokümandaki `AddJwtBearer()` örneği bu sample'ın kapsamı dışında.
