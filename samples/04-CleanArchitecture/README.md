# 04 — Clean Architecture + DDD Taktik Desenleri

İlgili dokümanlar:
- [Clean Architecture](../../01-uygulama-ici-mimariler/04-clean-architecture.md)
- [Domain-Driven Design](../../01-uygulama-ici-mimariler/06-domain-driven-design.md)

Jason Taylor / Ardalis şablonlarındaki .NET yorumunu (Domain / Application / Infrastructure / Api + MediatR
pipeline) DDD'nin taktik yapı taşlarıyla (Aggregate, Value Object, Strongly-typed ID, Domain Event) birleştiren,
derlenen ve testleri geçen bir örnek.

## Bu örnekte ne görüyorsunuz

- **The Dependency Rule**: `Domain` hiçbir şeye bağımlı değil (NuGet bile yok); `Application` sadece `Domain`'i,
  `Infrastructure` sadece `Application`'ı bilir. Kural `tests/Clean.ArchitectureTests` ile **otomatik denetlenir**.
- **Use case'ler görünür ("Screaming")**: `Application/Orders/Commands/PlaceOrder/` klasörüne bakınca sistemin ne
  yaptığı anlaşılır. Her use case = Command + Handler + Validator.
- **MediatR pipeline**: `LoggingBehaviour → ValidationBehaviour → Handler`. Cross-cutting kodlar handler'lara sızmaz.
- **Zengin domain modeli**: `Order.Place(...)`, `order.Ship(...)`, `order.Cancel(...)` — public setter yok,
  invariant'lar aggregate'te, ihlal → `DomainException` → **409**.
- **Domain event akışı**: `Order.Place` → `OrderPlaced` event'i → EF Core interceptor yayınlar →
  `OrderPlacedHandler` **Product aggregate'inin** stoğunu düşer (aynı transaction'da).
- **Merkezi hata çevirisi**: `GlobalExceptionHandler` (IExceptionHandler) → `ProblemDetails`
  (Validation→400, NotFound→404, Domain→409).

## Klasör / dosya ağacı

```
04-CleanArchitecture/
├── CleanArchitecture.slnx
├── src/
│   ├── Clean.Domain/                        ◉ Enterprise Business Rules (paket YOK)
│   │   ├── Common/
│   │   │   ├── Entity.cs                    Id'ye göre eşitlik
│   │   │   ├── AggregateRoot.cs             Domain event biriktirme (Raise/ClearDomainEvents) + IHasDomainEvents
│   │   │   ├── IDomainEvent.cs              Marker arayüz (MediatR'a bağımlı DEĞİL)
│   │   │   ├── DomainException.cs           İş kuralı ihlali (Error taşır)
│   │   │   └── Error.cs                     Error + Result / Result<T>
│   │   ├── Orders/
│   │   │   ├── Order.cs                     Aggregate root: Place / Ship / Cancel, invariant'lar
│   │   │   ├── OrderLine.cs                 İç entity (ctor internal → sadece root oluşturur), ad/fiyat snapshot
│   │   │   ├── OrderItem.cs                 Place factory girdisi
│   │   │   ├── OrderId.cs, CustomerId.cs    Strongly-typed ID (readonly record struct)
│   │   │   ├── Money.cs                     Value object (+, *, para birimi kontrolü)
│   │   │   ├── OrderStatus.cs, OrderErrors.cs
│   │   │   └── Events/OrderPlaced.cs, OrderShipped.cs, OrderCancelled.cs
│   │   └── Products/
│   │       ├── Product.cs                   Ayrı aggregate: DecreaseStock / IncreaseStock
│   │       └── ProductId.cs
│   ├── Clean.Application/                   ◉ Application Business Rules (Use Cases)
│   │   ├── Abstractions/                    Portlar: IApplicationDbContext, IDateTimeProvider
│   │   ├── Common/
│   │   │   ├── Behaviours/                  LoggingBehaviour, ValidationBehaviour (IPipelineBehavior)
│   │   │   ├── Events/                      DomainEventNotification<T> (domain event → INotification adaptörü)
│   │   │   └── Exceptions/                  NotFoundException, ValidationException
│   │   ├── Orders/
│   │   │   ├── Commands/PlaceOrder|ShipOrder|CancelOrder/   Command + Handler + Validator
│   │   │   ├── Queries/GetOrderById|ListOrders/             AsNoTracking + projeksiyon → OrderDto
│   │   │   └── EventHandlers/               OrderPlacedHandler (stok düş), OrderCancelledHandler (stok iade), OrderShippedHandler (log)
│   │   ├── Products/Queries/ListProducts/
│   │   └── DependencyInjection.cs           AddApplication(): MediatR + behaviour'lar + validator'lar
│   ├── Clean.Infrastructure/                ◉ Frameworks & Drivers (DB, saat)
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs              IApplicationDbContext implementasyonu
│   │   │   ├── Configurations/              Strongly-typed id dönüşümü, Money complex type, _lines backing field
│   │   │   ├── Interceptors/DispatchDomainEventsInterceptor.cs   SaveChanges ÖNCESİ event yayını
│   │   │   └── Seed/AppDbContextSeed.cs     EnsureCreated + 3 ürün
│   │   ├── Services/DateTimeProvider.cs
│   │   └── DependencyInjection.cs           AddInfrastructure(config)
│   └── Clean.Api/                           ◉ Interface Adapters + Composition Root
│       ├── Endpoints/OrderEndpoints.cs, ProductEndpoints.cs    Minimal API → ISender
│       ├── Infrastructure/GlobalExceptionHandler.cs           Exception → ProblemDetails
│       ├── Program.cs                       DI kompozisyonu, port 5004
│       └── Clean.Api.http                   Tüm endpoint'ler için örnek istekler
└── tests/
    ├── Clean.Domain.UnitTests/              Order invariant'ları, Money, domain event raise
    ├── Clean.Application.UnitTests/         Handler'lar (gerçek MediatR + SQLite in-memory), validator'lar
    ├── Clean.ArchitectureTests/             NetArchTest: katman bağımlılıkları + konvansiyonlar
    └── Clean.Api.IntegrationTests/          WebApplicationFactory, her test ayrı SQLite dosyası
```

## Clean halkaları ↔ projeler

| Clean halkası (doküman) | Bu örnekte | İçerik |
|---|---|---|
| Enterprise Business Rules (Entities) — sarı | `Clean.Domain` | Aggregate'ler, VO'lar, domain event'ler, `DomainException` |
| Application Business Rules (Use Cases) — kırmızı | `Clean.Application` | Command/Query + Handler'lar, portlar (`IApplicationDbContext`), behaviour'lar |
| Interface Adapters (Controllers, Presenters, Gateways) — yeşil | `Clean.Api/Endpoints`, `GlobalExceptionHandler`; `Clean.Infrastructure/Persistence/Configurations` | HTTP ↔ Command/DTO çevirisi, hata → ProblemDetails, nesne ↔ tablo eşlemesi |
| Frameworks & Drivers — mavi | `Clean.Infrastructure` (EF Core, SQLite), ASP.NET Core host (`Program.cs`) | Veritabanı, web sunucusu, saat |

> .NET şablonlarında "Interface Adapters" ayrı bir proje değildir; Api ve Infrastructure arasında paylaşılır.
> Önemli olan halka sayısı değil, **bağımlılıkların içeri bakması**dır.

## DDD yapı taşları ↔ dosyalar

| Yapı taşı | Dosya | Not |
|---|---|---|
| **Entity** | `Domain/Common/Entity.cs`, `Domain/Orders/OrderLine.cs` | Kimlik eşitliği; `OrderLine` sadece root üzerinden oluşur (`internal` ctor) |
| **Value Object** | `Domain/Orders/Money.cs` | `sealed record`, değişmez, para birimi uyuşmazlığında `DomainException` |
| **Strongly-typed ID** | `OrderId.cs`, `CustomerId.cs`, `ProductId.cs` | `readonly record struct`; EF'te `HasConversion` |
| **Aggregate / Aggregate Root** | `Domain/Orders/Order.cs`, `Domain/Products/Product.cs` | `AggregateRoot<TId>`; aggregate'ler arası **sadece ID** referansı |
| **Factory** | `Order.Place(...)`, `Product.Create(...)` | Geçersiz nesne hiç oluşamaz |
| **Domain Event** | `Domain/Orders/Events/*.cs` | `IDomainEvent` (paketsiz); `DomainEventNotification<T>` ile MediatR'a köprülenir |
| **Domain Service** | *Yok* | Stok düşme Product'ın kendi kuralı; iki aggregate arası koordinasyon event handler'da. Gerçekten birden fazla aggregate'i aynı anda hesaba katan *saf* bir kural (ör. kampanya fiyatlandırma) olsaydı `Domain/Services/` altında stateless bir sınıf olurdu. |
| **Repository** | *Yerine* `Application/Abstractions/IApplicationDbContext.cs` | Aggregate başına repository yerine `DbSet<T>` (aşağıdaki "pragmatik taviz") |
| **Ubiquitous Language** | `OrderErrors.cs`, metot adları | `order.Status = 3` değil `order.Ship(trackingNo)` |

### Aggregate sınırı ve snapshot
`Order`, `Product`'a nesne referansı tutmaz; `OrderLine` içinde `ProductId` + sipariş anındaki `ProductName` ve
`UnitPrice` **kopyalanır**. Ürünün fiyatı yarın değişse de dünkü siparişin toplamı değişmez, ve `Order`'ı yüklemek
`Product`'ı yüklemeyi gerektirmez (Vernon kuralı #3).

## Proje referans grafiği

```
               ┌──────────────┐
               │  Clean.Api   │  (composition root)
               └──┬────────┬──┘
                  │        │
                  ▼        ▼
 ┌────────────────────┐  ┌──────────────────────┐
 │ Clean.Application  │◄─┤ Clean.Infrastructure │
 └─────────┬──────────┘  └──────────────────────┘
           ▼
 ┌────────────────────┐
 │    Clean.Domain    │  → ∅ (hiçbir şey)
 └────────────────────┘
```

**Neden bu yönde?**
- **Kontrol akışı** dışarı gider (handler → veritabanı) ama **kaynak kod bağımlılığı** içeri bakar: Application
  `IApplicationDbContext`'i *tanımlar*, Infrastructure *uygular* (Dependency Inversion). Böylece SQLite'ı Postgres'e
  çevirmek Application'a dokunmaz.
- `Api → Infrastructure` referansı **sadece** `AddInfrastructure()` çağrısı için vardır. Endpoint'lerin Infrastructure'ı
  veya EF Core'u kullanmadığı bir mimari testle (`ApiEndpoints_ShouldNotDependOn_InfrastructureOrEfCore`) garanti altında.
- Domain en içte ve en kararlı katmandır; framework sürümleri değiştikçe en az değişmesi gereken yer.

### Pragmatik taviz: `IApplicationDbContext` ve `DbSet<T>`
`Clean.Application`, `Microsoft.EntityFrameworkCore` paketine referans verir; çünkü `IApplicationDbContext`
`DbSet<Order>` sunar ve handler'lar LINQ + `AsNoTracking`/`ToListAsync` kullanır. Bu, "Application dış dünyayı
bilmez" kuralını esnetir — ama **sağlayıcıyı (SQLite) bilmez**, sadece EF Core soyutlamasını bilir.
- **Kazanç**: Repository-over-EF tekrarından kurtulma, query'lerde doğrudan projeksiyon, daha az dosya.
- **Purist alternatif**: Application'da `IOrderRepository { Task<Order?> GetAsync(OrderId); void Add(Order); }`,
  `IProductRepository`, `IUnitOfWork { Task SaveChangesAsync(); }` ve okuma tarafı için `IOrderReadService`
  (DTO döner) tanımlanır; Application'dan EF Core paketi tamamen kalkar, hepsi Infrastructure'da implemente edilir.
  Bedeli: daha fazla arayüz/dosya ve sorgu esnekliğinin azalması.

## Domain event dispatch zamanlaması

`DispatchDomainEventsInterceptor`, event'leri **`SavingChangesAsync` içinde, yani SaveChanges'ten ÖNCE** yayınlar:

1. Handler `db.Orders.Add(order)` + `SaveChangesAsync()` çağırır.
2. Interceptor, change tracker'daki aggregate'lerden `OrderPlaced`'i toplar, temizler ve MediatR ile yayınlar.
3. `OrderPlacedHandler` **aynı scope'taki aynı DbContext** üzerinden ürünleri bulur ve `DecreaseStock` çağırır.
4. Interceptor `DetectChanges()` yapar; EF Core Order INSERT + Product UPDATE'lerini **tek transaction'da** yazar.

Neden önce? Stok düşme, siparişle **atomik** olmalı: stok yetmezse (`Product.InsufficientStock`) exception fırlar,
hiçbir şey yazılmaz ve kullanıcı 409 alır. "Sonra" (`SavedChangesAsync`) seçseydik sipariş çoktan kaydedilmiş olurdu ve
stok hatasında telafi (compensation) gerekirdi.

Bedeli: Bu handler'lar henüz commit edilmemiş veriyi görür; **e-posta göndermek, mesaj kuyruğuna yazmak gibi geri
alınamaz yan etkiler için uygun değildir** (transaction sonra patlarsa e-posta gitmiş olur). Onlar için
`SavedChangesAsync` + **Outbox deseni** (event'i aynı transaction'da bir tabloya yaz, arka planda yayınla) doğru
çözümdür. Not: Bu tercih DDD'nin "bir transaction'da tek aggregate değiştir" kuralını bilinçli olarak esnetir;
eventual consistency istenirse Outbox ile stok güncellemesi ayrı transaction'a taşınır.

## Bir isteğin yolculuğu

### `POST /orders` (event akışı dahil)

```
Endpoint → ISender → LoggingBehaviour → ValidationBehaviour → Handler → Domain → DbContext → Interceptor → EventHandler
```

| # | Dosya | Ne olur |
|---|---|---|
| 1 | `src/Clean.Api/Endpoints/OrderEndpoints.cs` | JSON → `PlaceOrderCommand`, `ISender.Send(...)` |
| 2 | `src/Clean.Application/Common/Behaviours/LoggingBehaviour.cs` | "➡️ PlaceOrderCommand başlıyor", süre ölçümü |
| 3 | `src/Clean.Application/Common/Behaviours/ValidationBehaviour.cs` | `PlaceOrderCommandValidator` çalışır; hata → `ValidationException` (400) |
| 4 | `src/Clean.Application/Orders/Commands/PlaceOrder/PlaceOrderCommandHandler.cs` | Ürünleri yükler (yoksa `NotFoundException`), snapshot `OrderItem`'lar hazırlar |
| 5 | `src/Clean.Domain/Orders/Order.cs` → `Place(...)` | Invariant'lar (1..10 kalem, miktar>0, tek para birimi), `OrderPlaced` raise |
| 6 | `src/Clean.Infrastructure/Persistence/AppDbContext.cs` | `Orders.Add`, `SaveChangesAsync` |
| 7 | `src/Clean.Infrastructure/Persistence/Interceptors/DispatchDomainEventsInterceptor.cs` | `OrderPlaced` → `DomainEventNotification<OrderPlaced>` → `IPublisher.Publish` |
| 8 | `src/Clean.Application/Orders/EventHandlers/OrderPlacedHandler.cs` | `Product.DecreaseStock(qty)` (yetersizse `DomainException` → hiçbir şey kaydedilmez) |
| 9 | EF Core | Order + OrderLines INSERT, Products UPDATE — tek transaction |
| 10 | `OrderEndpoints.cs` | `201 Created` + `{ "id": "..." }` |

### `POST /orders/{id}/cancel`

| # | Dosya | Ne olur |
|---|---|---|
| 1 | `src/Clean.Api/Endpoints/OrderEndpoints.cs` | Route `id` + opsiyonel `{ "reason": "..." }` → `CancelOrderCommand` → `ISender.Send` |
| 2 | `src/Clean.Application/Common/Behaviours/LoggingBehaviour.cs` | İstek loglanır |
| 3 | `src/Clean.Application/Common/Behaviours/ValidationBehaviour.cs` | `CancelOrderCommandValidator`: `OrderId` ve `Reason` dolu mu? |
| 4 | `src/Clean.Application/Orders/Commands/CancelOrder/CancelOrderCommandHandler.cs` | `db.Orders.FindAsync(new OrderId(id))` (yoksa `NotFoundException` → 404) |
| 5 | `src/Clean.Domain/Orders/Order.cs` → `Cancel(reason)` | `CanCancel()`: `Shipped` → `Order.ShippedCannotBeCancelled`, `Cancelled` → `Order.AlreadyCancelled` → `DomainException`. Başarılıysa `Status = Cancelled`, `OrderCancelled` raise |
| 6 | `src/Clean.Infrastructure/Persistence/Interceptors/DispatchDomainEventsInterceptor.cs` | `SaveChangesAsync` sırasında `OrderCancelled` yayınlanır |
| 7 | `src/Clean.Application/Orders/EventHandlers/OrderCancelledHandler.cs` | Her satır için `Product.IncreaseStock(qty)` (stok iadesi) |
| 8 | EF Core (`AppDbContext`) | Order UPDATE + Products UPDATE, tek transaction |
| 9 | `OrderEndpoints.cs` → `204 No Content` · Hata durumunda `src/Clean.Api/Infrastructure/GlobalExceptionHandler.cs` | `DomainException` → **409** `ProblemDetails` (`"code": "Order.ShippedCannotBeCancelled"`) |

## Çalıştırma

```bash
cd samples/04-CleanArchitecture
dotnet build
dotnet test                                   # 4 test projesi (domain, application, mimari, entegrasyon)
dotnet run --project src/Clean.Api            # http://localhost:5004
```

Veritabanı: `src/Clean.Api` altında `clean-architecture.db` (SQLite). **Migration yok**; açılışta
`EnsureCreated()` + seed (öğretim amaçlı sadelik). Sıfırlamak için dosyayı silin.

```bash
B=http://localhost:5004
curl -s $B/products

# Sipariş ver → 201 {"id":"..."}
ID=$(curl -s -X POST $B/orders -H 'Content-Type: application/json' \
  -d '{"customerId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","items":[{"productId":"11111111-1111-1111-1111-111111111111","quantity":2}]}' \
  | sed -E 's/.*"id":"([^"]+)".*/\1/')

curl -s $B/orders/$ID
curl -i -X POST $B/orders/$ID/ship -H 'Content-Type: application/json' -d '{"trackingNumber":"TRK-1"}'   # 204
curl -i -X POST $B/orders/$ID/cancel                                                                    # 409
curl -s $B/products                                   # Klavye stoğu 10 → 8 (OrderPlacedHandler)

# Yetersiz stok → 409, sipariş de oluşmaz
curl -i -X POST $B/orders -H 'Content-Type: application/json' \
  -d '{"customerId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","items":[{"productId":"33333333-3333-3333-3333-333333333333","quantity":99}]}'
```

Konsolda `LoggingBehaviour` ve event handler logları (➡️ ✅ 📦 🚚 ↩️) akışı adım adım gösterir.
Daha fazla örnek: `src/Clean.Api/Clean.Api.http`.

## Deneyin

1. **Purist Clean**: `IOrderRepository` + `IUnitOfWork` yazıp `IApplicationDbContext`'i kaldırın; ardından
   `Clean.Application.csproj`'dan `Microsoft.EntityFrameworkCore` referansını silin. Hangi dosyalar değişti?
   Mimari testlere "Application EF Core'a bağımlı olmamalı" kuralını ekleyin.
2. **Dispatch zamanını değiştirin**: Interceptor'da `SavingChangesAsync` yerine `SavedChangesAsync` kullanın.
   `Place_InsufficientStock_Returns409_AndNothingIsSaved` testi neden kırılıyor? Outbox nasıl çözerdi?
3. **Yeni use case**: `Order.AddLine(...)` (sadece `Placed` iken, max 10 kalem) ekleyin ve
   `POST /orders/{id}/lines` endpoint'ini Command + Handler + Validator olarak yazın. Önce domain testini yazın.
4. **Mimariyi bozun**: `Clean.Domain`'e `MediatR` paketi ekleyip `IDomainEvent : INotification` yapın ve
   `dotnet test tests/Clean.ArchitectureTests` çıktısını inceleyin.

## Bu örnekteki gözlemler

**Artılar**
- ✅ İş kuralları tek yerde (`Order.cs`, `Product.cs`), saf C# ile milisaniyede test ediliyor.
- ✅ Bağımlılık kuralı testle korunuyor; "yanlışlıkla" Infrastructure'a bağlanmak build'i kırıyor.
- ✅ Cross-cutting (log, validation, hata çevirisi) handler'lardan tamamen ayrık.
- ✅ Domain event'leri sayesinde Order ve Product birbirini tanımadan koordine oluyor.

**Eksiler**
- ❌ Basit bir "iptal et" özelliği 4 projede ~6 dosyaya yayılıyor (Command, Validator, Handler, Domain metodu, Endpoint, event handler).
- ❌ Zengin modelin ORM eşlemesi ek emek istiyor: private ctor, `_lines` backing field, id converter'ları,
  complex type, SQLite'ın `decimal`/`DateTime` kısıtları.
- ❌ MediatR "sihri" akışı gizleyebiliyor; F12 ile handler'a gidilemez (bu yüzden "isteğin yolculuğu" tablosu var).
- ❌ `IApplicationDbContext` tavizi saf Clean'i esnetiyor; event'lerin SaveChanges öncesi işlenmesi tek-aggregate-per-transaction kuralını esnetiyor.
