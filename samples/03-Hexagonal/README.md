# 03 — Hexagonal Mimari (Ports & Adapters)

📖 İlgili doküman: [../../01-uygulama-ici-mimariler/03-hexagonal-mimari.md](../../01-uygulama-ici-mimariler/03-hexagonal-mimari.md)

## Bu örnekte ne görüyorsunuz

- **Application Core** (`Hexagonal.Core`): Domain + **Port**'lar + Use case'ler. Hiçbir adapter'a, framework'e, NuGet paketine bağımlı değil.
- **Inbound (driving) port'lar** `Ports/In/` — `IPlaceOrderUseCase`, `ICancelOrderUseCase`, `IShipOrderUseCase`, `IGetOrderQuery` (+ `IListProductsQuery`).
- **Outbound (driven) port'lar** `Ports/Out/` — `IOrderRepository`, `IProductCatalog`, `IPaymentGateway`, `INotifier`.
- **Aynı use case, iki farklı driving adapter**: `POST /orders` (Web) ve kuyruk mesajı (Messaging → `PlaceOrderConsumer`) ikisi de `IPlaceOrderUseCase`'i çağırır.
- **Driven adapter'lar**: EF Core/SQLite (Persistence), sahte ödeme (Payments — 100.000 TRY üstünü reddeder), log bildirimi (Notifications).
- **Testler de bir adapter'dır**: `Hexagonal.Core.Tests` core'u in-memory test adapter'larıyla (`InMemoryOrderRepository`, `AlwaysDeclinePaymentGateway`…) sürer.

## Klasör / dosya ağacı

```
03-Hexagonal/
├── Hexagonal.slnx
├── src/
│   ├── Hexagonal.Core/                         ⬡ ALTIGENİN İÇİ — referans: YOK
│   │   ├── Domain/Order.cs                     Place/MarkPaid/Ship/Cancel kuralları
│   │   ├── Domain/OrderLine.cs, OrderStatus.cs, DomainException.cs
│   │   ├── Ports/In/                           ◀ inbound (driving) port'lar
│   │   │   ├── IPlaceOrderUseCase.cs           + PlaceOrderCommand
│   │   │   ├── ICancelOrderUseCase.cs, IShipOrderUseCase.cs
│   │   │   ├── IGetOrderQuery.cs               + OrderView
│   │   │   └── IListProductsQuery.cs           + ProductView
│   │   ├── Ports/Out/                          ▶ outbound (driven) port'lar
│   │   │   ├── IOrderRepository.cs, IProductCatalog.cs
│   │   │   ├── IPaymentGateway.cs              + PaymentResult
│   │   │   └── INotifier.cs
│   │   ├── UseCases/                           inbound port implementasyonları
│   │   │   ├── PlaceOrderService.cs            katalog → Order.Place → ödeme çek → kaydet → bildir
│   │   │   ├── CancelOrderService.cs, ShipOrderService.cs
│   │   │   └── GetOrderQueryService.cs, ListProductsQueryService.cs
│   │   └── Exceptions/                         OrderNotFoundException, PaymentDeclinedException
│   ├── Hexagonal.Adapters.Web/                 DRIVING: HTTP
│   │   ├── OrdersEndpoints.cs, ProductsEndpoints.cs   sadece Ports/In çağırır
│   │   ├── Contracts/PlaceOrderRequest.cs      HTTP'ye özgü istek modeli → PlaceOrderCommand
│   │   ├── CoreExceptionHandler.cs             Core exception → ProblemDetails (404/402/409)
│   │   └── WebAdapterExtensions.cs
│   ├── Hexagonal.Adapters.Messaging/           DRIVING: kuyruk
│   │   ├── InMemoryOrderQueue.cs               System.Threading.Channels
│   │   ├── PlaceOrderConsumer.cs               BackgroundService → IPlaceOrderUseCase
│   │   └── PlaceOrderMessage.cs
│   ├── Hexagonal.Adapters.Persistence/         DRIVEN: EF Core + SQLite
│   │   ├── HexagonalDbContext.cs, ProductRecord.cs
│   │   ├── Configurations/                     OrderConfiguration (backing field), ProductRecordConfiguration (seed)
│   │   ├── EfOrderRepository.cs                : IOrderRepository
│   │   └── EfProductCatalog.cs                 : IProductCatalog
│   ├── Hexagonal.Adapters.Payments/            DRIVEN: FakePaymentGateway (> 100.000 TRY → red)
│   ├── Hexagonal.Adapters.Notifications/       DRIVEN: LoggerNotifier (ILogger)
│   └── Hexagonal.Host/                         WIRING
│       ├── Program.cs                          tüm port ↔ adapter eşlemeleri + POST /queue/orders simülasyonu
│       ├── appsettings.json                    Urls: http://localhost:5003
│       └── Hexagonal.Host.http
└── tests/
    └── Hexagonal.Core.Tests/
        ├── TestAdapters/                       InMemoryOrderRepository, InMemoryProductCatalog,
        │                                       AlwaysApprove/AlwaysDeclinePaymentGateway, RecordingNotifier
        ├── PlaceOrderUseCaseTests.cs
        └── ShipAndCancelUseCaseTests.cs
```

## Driving vs Driven — diyagram

```
        DRIVING (Primary) taraf                                       DRIVEN (Secondary) taraf
     uygulamayı TETİKLEYENLER                                       uygulamanın KULLANDIKLARI

                                     ┌───────────────────────────────┐
 [Web: OrdersEndpoints] ──┐          │        Hexagonal.Core         │        ┌──► [Persistence: EfOrderRepository] ──► SQLite
   POST /orders           │          │                               │        │      (IOrderRepository)
                          ├──► Ports/In ──► UseCases ──► Ports/Out ───┼────────┼──► [Persistence: EfProductCatalog]
 [Messaging:              │  IPlaceOrderUseCase    Domain/Order      │        │      (IProductCatalog)
  PlaceOrderConsumer] ────┤  ICancelOrderUseCase                     │        ├──► [Payments: FakePaymentGateway]
   kuyruk mesajı          │  IShipOrderUseCase                       │        │      (IPaymentGateway)
                          │  IGetOrderQuery                          │        └──► [Notifications: LoggerNotifier] ──► konsol
 [xUnit: Core.Tests] ─────┘                                          │               (INotifier)
                                     └───────────────────────────────┘
   Driving adapter port'u ÇAĞIRIR.                                  Driven adapter port'u IMPLEMENTE EDER.
   (Core, onu kimin çağırdığını bilmez)                             (Core, implementasyonun ne olduğunu bilmez)
```

Testlerde sağ taraf değişir: `EfOrderRepository` yerine `InMemoryOrderRepository`, `FakePaymentGateway` yerine
`AlwaysDeclinePaymentGateway`… Core'da tek satır değişmez.

## Proje referans grafiği

```
                            ┌──────────────── Hexagonal.Host (wiring) ────────────────┐
                            │         │            │             │           │        │
                            ▼         ▼            ▼             ▼           ▼        ▼
          Adapters.Web  Adapters.Messaging  Adapters.Persistence  Adapters.Payments  Adapters.Notifications
           (ASP.NET)     (Hosting.Abstr.)       (EF Core)                              (Logging.Abstr.)
                │              │                    │                   │                  │
                └──────────────┴────────────┬───────┴───────────────────┴──────────────────┘
                                            ▼
                                     Hexagonal.Core   ◄── Hexagonal.Core.Tests
                                  (referans: YOK)
```

**Neden bu yönde?** Tüm adapter'lar — ister driving ister driven olsun — **Core'a** bağımlıdır; Core hiçbirine
bağımlı değildir. Driving adapter'lar Core'un inbound port'larını *kullanır*, driven adapter'lar Core'un outbound
port'larını *implemente eder*. Adapter'lar birbirini tanımaz (Web, Persistence'ı bilmez). Hepsini birleştiren tek
yer `Hexagonal.Host/Program.cs`.

## Bir isteğin yolculuğu: `POST /orders/{id}/cancel`

1. **`src/Hexagonal.Adapters.Web/OrdersEndpoints.cs`** (driving adapter) → `MapPost("/{id:guid}/cancel")`
   → **`ICancelOrderUseCase`** inbound port'unu çağırır (`src/Hexagonal.Core/Ports/In/ICancelOrderUseCase.cs`).
2. DI (`src/Hexagonal.Host/Program.cs`) bu port için **`src/Hexagonal.Core/UseCases/CancelOrderService.cs`**'i verir.
3. Use case outbound port **`IOrderRepository`**'den siparişi ister (`src/Hexagonal.Core/Ports/Out/IOrderRepository.cs`)
   → driven adapter **`src/Hexagonal.Adapters.Persistence/EfOrderRepository.cs`** → `HexagonalDbContext` → SQLite (`hexagonal.db`).
4. Kural domain'de: **`src/Hexagonal.Core/Domain/Order.cs`** → `Cancel()` (`Shipped` ise `DomainException`).
5. `IOrderRepository.SaveChangesAsync()` → ardından outbound port **`INotifier`** → driven adapter
   **`src/Hexagonal.Adapters.Notifications/LoggerNotifier.cs`** konsola `🔔 ... Sipariş iptal edildi.` yazar.
6. Web adapter `204` döner. Exception varsa **`src/Hexagonal.Adapters.Web/CoreExceptionHandler.cs`** HTTP'ye çevirir:
   `DomainException` → 409, `OrderNotFoundException` → 404, `PaymentDeclinedException` → 402.

### Bonus: aynı use case'in kuyruktan tetiklenmesi (`POST /queue/orders`)

`Host/Program.cs` (dış sistem simülasyonu) mesajı **`InMemoryOrderQueue`**'ya koyar ve hemen `202` döner →
**`src/Hexagonal.Adapters.Messaging/PlaceOrderConsumer.cs`** (BackgroundService) mesajı okur → `PlaceOrderCommand`'a
çevirir → **aynı `IPlaceOrderUseCase`** → aynı `PlaceOrderService`. Hata HTTP'ye değil log'a (`📨 ... reddedildi`) yansır.

## Çalıştırma

```bash
cd samples/03-Hexagonal
dotnet build
dotnet test
dotnet run --project src/Hexagonal.Host     # http://localhost:5003 (hexagonal.db çalışma dizininde oluşur)
```

> Not: Öğretim amaçlı sadelik için **migration yok**; açılışta `EnsureCreated()` + `HasData` seed çalışır.

```bash
curl http://localhost:5003/products

# Driving adapter #1: HTTP
curl -i -X POST http://localhost:5003/orders -H "Content-Type: application/json" \
  -d '{"customerId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","items":[{"productId":"11111111-1111-1111-1111-111111111111","quantity":2}]}'
# → 201 {"id":"<ID>"}

curl http://localhost:5003/orders/<ID>                       # paymentTransactionId dolu
curl -i -X POST http://localhost:5003/orders/<ID>/ship       # 204
curl -i -X POST http://localhost:5003/orders/<ID>/cancel     # 409

# Ödeme reddi: 16 x 6500 = 104.000 TRY > 100.000 → 402
curl -i -X POST http://localhost:5003/orders -H "Content-Type: application/json" \
  -d '{"customerId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","items":[{"productId":"33333333-3333-3333-3333-333333333333","quantity":16}]}'

# Driving adapter #2: kuyruk (aynı use case) → 202, sonra GET /orders'ta görün
curl -i -X POST http://localhost:5003/queue/orders -H "Content-Type: application/json" \
  -d '{"customerId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","items":[{"productId":"22222222-2222-2222-2222-222222222222","quantity":3}]}'
curl http://localhost:5003/orders
```

Tüm istekler: `src/Hexagonal.Host/Hexagonal.Host.http`.

## Deneyin

1. **Üçüncü driving adapter:** `Hexagonal.Adapters.Cli` adında bir console projesi açıp `IPlaceOrderUseCase`'i
   komut satırından çağırın. Core'a dokunmanız gerekti mi?
2. **Driven adapter değiştirin:** `Hexagonal.Adapters.Payments` içine limit 10.000 TRY olan `StrictPaymentGateway`
   yazın ve `Program.cs`'te tek satırı değiştirin. Hangi testler etkilendi? (İpucu: hiçbiri — testler kendi adapter'larını kullanır.)
3. **İade akışı:** `IPaymentGateway`'e `RefundAsync` ekleyin ve `CancelOrderService` iptalde iade yapsın. Test için
   `RecordingPaymentGateway` test adapter'ı yazın.

## Gözlemler (bu örnekte)

- ✅ Aynı `PlaceOrderService` HTTP'den, kuyruktan ve testlerden **değiştirilmeden** tetiklendi.
- ✅ Ödeme reddi gibi dış sistem senaryoları test adapter'ı ile deterministik (`AlwaysDeclinePaymentGateway`).
- ✅ "Üst/alt katman" yok; DB de UI de "dış dünya". Teknoloji değişimi = adapter değişimi.
- ❌ 7 üretim projesi + çok sayıda port; bu boyuttaki bir uygulama için fazla tören.
- ❌ Hexagonal içerinin (domain/use case) nasıl organize edileceğini söylemez; burada Onion'a benzer bir rich model seçildi.
- ℹ️ Basitlik için domain kuralı ihlalleri 409, ödeme reddi 402 döner. Kuyruk process içidir (uygulama kapanınca mesajlar kaybolur).
