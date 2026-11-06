# 09 — Saga: Choreography (Koreografi)

📖 İlgili dokümanlar:
- [Saga Deseni](../../03-dagitik-desenler/01-saga.md) — özellikle **"A) Choreography"** bölümü
- [Transactional Outbox & Inbox](../../03-dagitik-desenler/02-outbox-inbox.md)

> ⚠️ **Öğretim amaçlı simülasyon.** Üç "servis" (Ordering, Inventory, Payment) **tek process** içinde çalışır;
> ama her biri ayrı klasörde, ayrı `DbContext` ve ayrı SQLite dosyasıyla yaşar ve birbirleriyle **sadece mesajlarla**
> konuşur. Broker olarak **MassTransit 8.5.5 in-memory transport** kullanılır → RabbitMQ/Docker kurmadan `dotnet run` yeter.
> Gerçekten ayrı process'lerde, RabbitMQ ile ve **orchestration** (MassTransit state machine) yaklaşımıyla dağıtık
> versiyon için `samples/10-Microservices` örneğine bakın.

## Bu örnekte ne görüyorsunuz

- **Merkezi koordinatör yok**: her servis bir olayı dinler, kendi yerel transaction'ını yapar, yeni bir olay yayınlar.
- **Compensation**: Payment başarısız olunca Inventory, `PaymentFailed` olayını dinleyip stoğu **iade eder**
  (silme/rollback değil, semantik geri alma).
- **Database-per-service**: `ordering.db`, `inventory.db`, `payment.db` — hiçbir servis diğerinin tablosunu okuyamaz.
  Ordering fiyatları bilmek için kendi `Catalog` kopyasını tutar.
- **Transactional Outbox** (MassTransit EF Outbox): `POST /orders`, siparişi ve `OrderCreated` mesajını **tek transaction**'da yazar.
- **Idempotent consumer (Inbox)**: her consumer işlediği `(MessageId, Consumer)` çiftini kendi DB'sindeki
  `ProcessedMessages` tablosuna iş verisiyle **aynı `SaveChanges`** içinde yazar; ek olarak doğal idempotency kullanılır.
- **Retry + in-memory outbox**: geçici hatalar/concurrency çakışmaları yeniden denenir; başarısız denemenin
  publish ettiği mesajlar hiç gönderilmez.

## Klasör / dosya ağacı

```
09-SagaChoreography/
├── SagaChoreography.slnx
├── src/SagaChoreography.Api/
│   ├── Program.cs                     # Host: servisleri + MassTransit (in-memory, retry, outbox) kaydeder, DB'leri oluşturur
│   ├── appsettings.json               # Port 5009 + servis başına connection string
│   ├── SagaChoreography.Api.http      # Üç senaryo + tüm endpoint'ler
│   ├── Contracts/Messages.cs          # OrderCreated, StockReserved, StockReservationFailed,
│   │                                  # PaymentCompleted, PaymentFailed, OrderCancelled (hepsi geçmiş zaman event)
│   ├── BuildingBlocks/
│   │   ├── ProcessedMessage.cs        # Inbox tablosu + IsAlreadyProcessedAsync / MarkAsProcessed
│   │   └── DbContextRegistration.cs   # Servis başına SQLite DbContext kaydı
│   └── Services/
│       ├── Ordering/
│       │   ├── Order.cs               # Pending → Confirmed/Rejected (+ Shipped/Cancelled), CatalogItem
│       │   ├── OrderingDbContext.cs   # ordering.db + MassTransit outbox tabloları
│       │   ├── OrderingEndpoints.cs   # POST/GET /orders, ship, cancel, GET /products  (saga'yı BAŞLATIR)
│       │   ├── OrderingService.cs     # AddOrderingService / AddOrderingConsumers
│       │   └── Consumers/OrderingConsumers.cs   # PaymentCompleted→Confirm, StockReservationFailed/PaymentFailed→Reject
│       ├── Inventory/
│       │   ├── Stock.cs               # Stock (optimistic concurrency) + Reservation (Reserved/Released)
│       │   ├── InventoryDbContext.cs  # inventory.db
│       │   ├── InventoryService.cs    # GET /inventory
│       │   └── Consumers/InventoryConsumers.cs  # OrderCreated→Reserve, PaymentFailed→Release (COMPENSATION), OrderCancelled→Release
│       └── Payment/
│           ├── Payment.cs             # Kural: toplam > 10.000 TRY → Failed ("Limit aşıldı")
│           ├── PaymentDbContext.cs    # payment.db (OrderId unique)
│           ├── PaymentService.cs      # GET /payments
│           └── Consumers/PaymentConsumers.cs    # StockReserved→Charge, OrderCancelled→Refund
└── tests/SagaChoreography.Tests/
    ├── SagaFactory.cs                 # WebApplicationFactory, test başına izole .db dosyaları
    └── ChoreographySagaTests.cs       # (a) Confirmed, (b) compensation→Rejected, (c) stok yok→Rejected, iptal/iade
```

## Referans grafiği (tek proje — sınırlar klasör + namespace ile)

```
SagaChoreography.Tests ──► SagaChoreography.Api ──► MassTransit 8.5.5, MassTransit.EntityFrameworkCore, EF Core Sqlite

İçeride (mantıksal bağımlılıklar):

   Services/Ordering ──┐
   Services/Inventory ─┼──► Contracts/Messages.cs   (ortak dil: sadece mesajlar)
   Services/Payment ───┘          ▲
                                  │  hiçbir servis diğer servisin klasörüne / DbContext'ine referans vermez
   BuildingBlocks (Inbox) ◄───────┘  (gerçekte ortak NuGet paketi olurdu)
```

**Neden bu yönde?** Servisler arasındaki tek ortak bilgi **mesaj sözleşmeleridir**. Inventory, Ordering'in `Order`
sınıfını bilmez; sadece `OrderCreated` mesajını bilir. Bu, servisleri ayrı process'lere taşırken (bkz. `10-Microservices`)
sadece `Contracts`'ın paylaşılan pakete dönüşmesi ve transport'un RabbitMQ olması gerektiği anlamına gelir.

## Akış — sekans diyagramları

### Mutlu yol: (a) 1 klavye → `Confirmed`

```
 Client          Ordering                 Inventory                 Payment
   │ POST /orders   │                         │                         │
   │───────────────►│ T1: Order(Pending)      │                         │
   │                │ + Outbox[OrderCreated]  │                         │
   │◄── 201 ────────│ (tek transaction)       │                         │
   │                │──── OrderCreated ──────►│ T2: Reserve (Klavye-1)  │
   │                │                         │──── StockReserved ─────►│ T3: Charge 750 ≤ 10.000 ✔
   │                │◄──────────────────────────────── PaymentCompleted ┤
   │                │ T4: Order → Confirmed ✅ │                         │
   │ GET /orders/id │                         │                         │
   │───────────────►│ "Confirmed"             │                         │
```

### Compensation yolu: (b) 2 monitör (13.000 TRY) → ödeme başarısız → stok iade → `Rejected`

```
 Client          Ordering                 Inventory                 Payment
   │ POST /orders   │                         │                         │
   │───────────────►│ T1: Order(Pending)      │                         │
   │◄── 201 ────────│                         │                         │
   │                │──── OrderCreated ──────►│ T2: Reserve (Monitör-2) │  stok 5 → 3
   │                │                         │──── StockReserved ─────►│ T3: 13.000 > 10.000 ❌
   │                │                         │                         │ Payment(Failed)
   │                │◄────────────────────────┼────── PaymentFailed ────┤ (yayın: iki dinleyici)
   │                │                         │◄───── PaymentFailed ────┤
   │                │ C1: Order → Rejected    │ C2: Release reservation │  stok 3 → 5 (COMPENSATION)
```

### (c) 50 klavye → stok yok → `Rejected` (ödeme hiç denenmez)

```
 Ordering ── OrderCreated ──► Inventory: 50 > 10 ❌ ── StockReservationFailed ──► Ordering: Rejected
                              (rezervasyon yok → telafi edilecek bir şey yok)
```

## Choreography'nin eksisi: "akış kodda dağınık"

"Bir sipariş hangi adımlardan geçer?" sorusunun cevabı **tek bir dosyada yazılı değil**. Akışı anlamak için şu
dosyaları sırayla okumanız gerekir:

| Adım | Nerede? |
|---|---|
| T1 — Sipariş Pending, `OrderCreated` yayınla | `src/SagaChoreography.Api/Services/Ordering/OrderingEndpoints.cs` → `PlaceOrder` |
| T2 — Stok rezerve / `StockReservationFailed` | `src/SagaChoreography.Api/Services/Inventory/Consumers/InventoryConsumers.cs` → `ReserveStockOnOrderCreatedConsumer` |
| T3 — Ödeme / `PaymentFailed` (limit kuralı) | `src/SagaChoreography.Api/Services/Payment/Consumers/PaymentConsumers.cs` + `Services/Payment/Payment.cs` |
| T4 — Onay | `src/SagaChoreography.Api/Services/Ordering/Consumers/OrderingConsumers.cs` → `ConfirmOrderOnPaymentCompletedConsumer` |
| C1 — Ret | aynı dosya → `RejectOrderOnPaymentFailedConsumer`, `RejectOrderOnStockReservationFailedConsumer` |
| C2 — Stok iadesi (compensation) | `src/SagaChoreography.Api/Services/Inventory/Consumers/InventoryConsumers.cs` → `ReleaseStockOnPaymentFailedConsumer` |

Dikkat edin:
- Payment, `PaymentFailed` yayınladığında **kimin telafi yapacağını bilmez**. Telafi mantığı Inventory'nin içinde "gizli".
  Yeni bir adım (ör. "kargo rezervasyonu") eklemek, birden fazla servisin consumer'larına dokunmayı gerektirir.
- "Bu sipariş şu an hangi adımda?" sorusunun cevabı üç ayrı veritabanına dağılmıştır
  (`GET /orders/{id}` + `GET /inventory` + `GET /payments`). Orchestration'da bu, saga state tablosunda tek satırdır.
- Inventory, Payment'ın olayını (`PaymentFailed`) dinliyor; Payment, Inventory'nin olayını (`StockReserved`) dinliyor →
  servisler arasında **döngüsel olay bağımlılığı**.

## Idempotency (Inbox) — nasıl çalışıyor?

Mesaj sistemleri **at-least-once** teslim eder; retry de aynı mesajı yeniden işletir. Bu örnekte üç katman var:

1. **Inbox tablosu** (`BuildingBlocks/ProcessedMessage.cs`): consumer başında
   `IsAlreadyProcessedAsync(context, this)` → `(MessageId, ConsumerAdı)` varsa atla. Sonda `MarkAsProcessed` +
   iş değişiklikleri **tek `SaveChanges`** (tek SQLite transaction). Composite PK, eşzamanlı ikinci denemeyi patlatır → retry → atlanır.
2. **Doğal idempotency**: `Order.Confirm()` / `Reject()` sadece `Pending` iken etkili; `StockReleaser` rezervasyon
   zaten `Released` ise tekrar iade etmez; `Payment.Refund()` sadece `Completed` iken çalışır.
3. **Unique constraint**: `Payments.OrderId` unique → sipariş başına iki ödeme imkânsız.

Ayrıca:
- **Producer tarafı**: `POST /orders` ve `/cancel`, MassTransit **EF bus outbox** ile `OrderCreated`/`OrderCancelled`'ı
  `ordering.db`'deki `OutboxMessage` tablosuna yazar (sipariş ile aynı transaction). Uygulama tam bu anda çökse bile
  mesaj kaybolmaz.
- **Consumer tarafı**: `UseInMemoryOutbox` — consumer içinde `Publish` edilen mesajlar, consumer **başarıyla** bitene
  kadar bekletilir. Kalan küçük açık: `SaveChanges` sonrası, mesaj gönderilmeden process çökerse mesaj kaybolabilir.
  Üretimde çözüm: consumer endpoint'lerinde `UseEntityFrameworkOutbox<TDbContext>(context)` (MassTransit'in kalıcı
  inbox+outbox'ı) — bu örnekte Inbox'ı görünür kılmak için elle yazdık.
- **Concurrency**: `Stock.Version` concurrency token; aynı ürüne eşzamanlı iki rezervasyon gelirse biri
  `DbUpdateConcurrencyException` alır ve `UseMessageRetry` ile yeniden işlenir.

## Bir isteğin yolculuğu: `POST /orders/{id}/cancel`

İptal, onaylanmış (Confirmed) bir sipariş için **ikinci bir choreography** başlatır (stok iadesi + para iadesi):

1. **`src/SagaChoreography.Api/Program.cs`** — `app.MapOrderingEndpoints()` route'u kaydetmişti.
2. **`src/SagaChoreography.Api/Services/Ordering/OrderingEndpoints.cs`** → `CancelOrder`: siparişi `OrderingDbContext`'ten yükler.
3. **`src/SagaChoreography.Api/Services/Ordering/Order.cs`** → `Order.Cancel()`:
   `Shipped`/`Cancelled`/`Rejected` → `OrderConflictException` → **409**; `Pending` → 409 ("saga henüz tamamlanmadı").
4. **`OrderingEndpoints.cs`** → `publisher.Publish(new OrderCancelled(...))` — MassTransit bus outbox sayesinde
   mesaj `OutboxMessage` tablosuna eklenir; `SaveChangesAsync()` sipariş durumu + outbox satırını **tek transaction**'da yazar → **204**.
5. MassTransit'in arka plan **outbox delivery servisi** satırı okuyup in-memory bus'a yayınlar (`Program.cs` → `AddEntityFrameworkOutbox`).
6. **`src/SagaChoreography.Api/Services/Inventory/Consumers/InventoryConsumers.cs`** → `ReleaseStockOnOrderCancelledConsumer`:
   Inbox kontrolü → `StockReleaser.ReleaseAsync` → rezervasyon `Released`, stok geri eklenir → `inventory.db`.
7. **`src/SagaChoreography.Api/Services/Payment/Consumers/PaymentConsumers.cs`** → `RefundPaymentOnOrderCancelledConsumer`:
   Inbox kontrolü → `Payment.Refund()` → `Refunded` → `payment.db`.
8. Her iki consumer, **`src/SagaChoreography.Api/BuildingBlocks/ProcessedMessage.cs`** ile işlenen mesajı kaydeder.

## Çalıştırma

```bash
cd samples/09-SagaChoreography
dotnet build
dotnet test                                     # 5 test: (a), (b), (c), iptal/iade, hatalı istekler
dotnet run --project src/SagaChoreography.Api  # http://localhost:5009
```

Veritabanları (`ordering.db`, `inventory.db`, `payment.db`) proje klasöründe `EnsureCreated()` ile oluşturulur ve seed edilir.
**Migration yok** (öğretim amaçlı sadelik). Temiz başlangıç için `.db` dosyalarını silin.

### curl senaryoları

```bash
B=http://localhost:5009
C=aaaaaaaa-0000-0000-0000-000000000001
order() { curl -s -X POST $B/orders -H 'Content-Type: application/json' \
  -d "{\"customerId\":\"$C\",\"items\":[{\"productId\":\"$1\",\"quantity\":$2}]}" | sed -E 's/.*"id":"([^"]+)".*/\1/'; }

# (a) Mutlu yol: 1 klavye → Confirmed
A=$(order 11111111-1111-1111-1111-111111111111 1); sleep 1
curl -s $B/orders/$A                      # "status":"Confirmed"

# (b) Compensation: 2 monitör = 13.000 TRY → PaymentFailed → stok iade → Rejected
Bo=$(order 33333333-3333-3333-3333-333333333333 2); sleep 1
curl -s $B/orders/$Bo                     # "status":"Rejected","statusReason":"Limit aşıldı: 13000.00 TRY > 10000.00 TRY"
curl -s $B/inventory                      # Monitör "available":5, rezervasyon "Released"
curl -s $B/payments                       # "status":"Failed"

# (c) Stok yok: 50 klavye → StockReservationFailed → Rejected
Co=$(order 11111111-1111-1111-1111-111111111111 50); sleep 1
curl -s $B/orders/$Co                     # "status":"Rejected","statusReason":"'Klavye' için yeterli stok yok."

# Kargola / iptal
curl -i -X POST $B/orders/$A/ship         # 204
curl -i -X POST $B/orders/$A/cancel       # 409 (kargolanmış)
D=$(order 22222222-2222-2222-2222-222222222222 2); sleep 1
curl -i -X POST $B/orders/$D/cancel       # 204 → stok + ödeme iadesi
sleep 1; curl -s $B/payments              # D'nin ödemesi "Refunded"
```

Konsol logunda adımları izleyebilirsiniz: `[Inventory] ... stok rezerve edildi`, `[Payment] ...: Failed Limit aşıldı`,
`[Inventory] COMPENSATION: ... stok iade edildi`.

## Deneyin

1. **Yeni adım ekleyin**: "Shipping" servisi `PaymentCompleted`'ı dinleyip kargo rezervasyonu yapsın, başarısız olursa
   `ShippingFailed` yayınlasın. Bu durumda **hem** ödeme iadesi **hem** stok iadesi gerekir — kaç dosyaya dokunmak
   zorunda kaldığınızı sayın. (Choreography'nin ölçeklenme sorunu.)
2. **Idempotency'yi ve kalan açığı görün**: `ReserveStockOnOrderCreatedConsumer`'da `SaveChangesAsync`'ten **sonra**
   bir kez exception fırlatan geçici kod ekleyin. Retry mesajı yeniden işler; Inbox sayesinde stok iki kez düşmez
   (`sqlite3 inventory.db "select * from ProcessedMessages"`). Ama sipariş sonsuza dek `Pending` kalır: ilk denemenin
   `StockReserved`'ı in-memory outbox tarafından atıldı, ikinci deneme ise "zaten işlendi" diye atladı. Çözüm:
   consumer endpoint'ine `UseEntityFrameworkOutbox<InventoryDbContext>(context)` ekleyip publish'i de aynı transaction'a almak.
3. Payment limitini `Payment.Limit` ile değiştirin ve (b) senaryosunun Confirmed'a döndüğünü görün. Sonra aynı akışı
   `10-Microservices` örneğindeki orchestration state machine ile karşılaştırın: akış orada **tek dosyada** okunuyor mu?

## Bu örnekteki gözlemler

**Artılar**
- ✅ Her servis sadece kendi işini biliyor; Payment, Ordering'den tamamen habersiz → maksimum gevşek bağlılık.
- ✅ Merkezi tek hata noktası (orchestrator) yok; 3 adımlık akış için çok az kod.
- ✅ Outbox + Inbox + retry ile "at-least-once + idempotent = effectively-once" uçtan uca görülebiliyor.

**Eksiler**
- ❌ Akış hiçbir yerde yazılı değil (yukarıdaki tablo); yeni geliştirici için öğrenme maliyeti yüksek.
- ❌ Sipariş durumu eventual consistent: `POST /orders` 201 döndüğünde sipariş hâlâ `Pending`; istemci polling yapmalı.
- ❌ `Pending` sipariş iptal edilemiyor (saga ortasında iptal, choreography'de ek olay/karmaşıklık gerektirir).
- ❌ Test etmek zor: birim test yerine olayların dolaşmasını bekleyen (polling) integration test gerekiyor.
- ❌ Tek process simülasyonu gerçek ağ hatalarını, broker kesintilerini ve servislerin bağımsız deploy'unu göstermez.
