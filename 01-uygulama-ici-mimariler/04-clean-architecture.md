# 4. Clean Architecture

> 💻 **Çalışan örnek proje:** [`samples/04-CleanArchitecture`](../samples/04-CleanArchitecture/)

> Robert C. Martin (Uncle Bob), 2012 (blog), 2017 (kitap)

## Neden Ortaya Çıktı?

2000'lerin sonunda Hexagonal, Onion, BCE (Boundary-Control-Entity), DCI gibi pek çok mimari aynı şeyi farklı isimlerle söylüyordu. Uncle Bob bunları tek bir modelde birleştirdi ve şu ek problemleri vurguladı:

- **"Screaming Architecture"**: Klasör yapınıza bakınca "bu bir Rails/ASP.NET uygulaması" değil, "bu bir sağlık sigortası sistemi" demeli.
- **Framework bağımlılığı**: Framework'ler araçtır, mimari değil. İş mantığı framework'e "evlenmemeli".
- **Use case'lerin görünmezliği**: Katmanlı mimaride sistemin ne yaptığı (use case'ler) dev servis sınıflarında kayboluyordu.

## Çözdüğü Problem

Bir sistem şunlardan **bağımsız** olmalı:
1. Framework'ler
2. UI
3. Veritabanı
4. Herhangi bir dış ajan
5. Ve **test edilebilir** olmalı.

## Yapı

```
   ┌────────────────────────────────────────────────────────┐
   │ Frameworks & Drivers (Web, DB, UI, Devices, External)  │  mavi
   │  ┌──────────────────────────────────────────────────┐  │
   │  │ Interface Adapters (Controllers, Presenters,     │  │  yeşil
   │  │                     Gateways/Repositories)       │  │
   │  │  ┌────────────────────────────────────────────┐  │  │
   │  │  │ Application Business Rules (Use Cases)     │  │  │  kırmızı
   │  │  │  ┌──────────────────────────────────────┐  │  │  │
   │  │  │  │ Enterprise Business Rules (Entities) │  │  │  │  sarı
   │  │  │  └──────────────────────────────────────┘  │  │  │
   │  │  └────────────────────────────────────────────┘  │  │
   │  └──────────────────────────────────────────────────┘  │
   └────────────────────────────────────────────────────────┘
```

### The Dependency Rule (Tek Kural)

> **Kaynak kod bağımlılıkları yalnızca içeri doğru olabilir.** İç halka dış halka hakkında hiçbir şey bilemez — isim, sınıf, fonksiyon, veri formatı dahil.

Kontrol akışı dışarı doğru gitse bile (use case → DB), **bağımlılık tersine çevrilir** (use case `IOrderRepository` tanımlar, Infrastructure implemente eder).

## .NET'te Yaygın Şablon (Jason Taylor / Ardalis)

```
src/
├── Domain/                 Entities, ValueObjects, Enums, DomainEvents, Exceptions
├── Application/            Use case'ler (Commands/Queries + Handlers), Interfaces,
│   ├── Common/             Behaviours (Validation, Logging), Mappings, Exceptions
│   └── Orders/
│       ├── Commands/CreateOrder/
│       │   ├── CreateOrderCommand.cs
│       │   ├── CreateOrderCommandHandler.cs
│       │   └── CreateOrderCommandValidator.cs
│       └── Queries/GetOrderById/
├── Infrastructure/         EF Core, Identity, dosya, e-posta, dış API'ler
└── Web (Api)/              Endpoints/Controllers, DI composition, middleware
tests/
├── Domain.UnitTests/
├── Application.UnitTests/
├── Application.FunctionalTests/
└── Architecture.Tests/
```

Referanslar: `Web → Application, Infrastructure` · `Infrastructure → Application` · `Application → Domain` · `Domain → ∅`

## Kod Örneği (MediatR ile)

```csharp
// ---------- Domain ----------
public class Order : BaseEntity
{
    public static Order Create(Guid customerId, IEnumerable<OrderItem> items)
    {
        var order = new Order { Id = Guid.NewGuid(), CustomerId = customerId };
        foreach (var i in items) order.AddItem(i);
        order.AddDomainEvent(new OrderCreatedEvent(order.Id));
        return order;
    }
    // ...
}

// ---------- Application ----------
public interface IApplicationDbContext
{
    DbSet<Order> Orders { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);
}

public record CreateOrderCommand(Guid CustomerId, List<OrderItemDto> Items) : IRequest<Guid>;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator() => RuleFor(x => x.Items).NotEmpty();
}

public class CreateOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateOrderCommand req, CancellationToken ct)
    {
        var order = Order.Create(req.CustomerId, req.Items.Select(i => new OrderItem(i.ProductId, i.Qty, i.Price)));
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        return order.Id;
    }
}

// Cross-cutting: Pipeline behaviour
public class ValidationBehaviour<TReq, TRes>(IEnumerable<IValidator<TReq>> validators)
    : IPipelineBehavior<TReq, TRes> where TReq : notnull
{
    public async Task<TRes> Handle(TReq req, RequestHandlerDelegate<TRes> next, CancellationToken ct)
    {
        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(req, ct))))
                       .SelectMany(r => r.Errors).ToList();
        if (failures.Count != 0) throw new ValidationException(failures);
        return await next();
    }
}

// ---------- Web ----------
app.MapPost("/orders", async (CreateOrderCommand cmd, ISender sender) => Results.Ok(await sender.Send(cmd)));
```

> Not: `IApplicationDbContext`'in `DbSet<T>` sunması saf Clean'e göre bir "pragmatik taviz"dir (Application, EF Core'u bilir). Purist yaklaşım repository arayüzleri kullanır. İkisi de yaygındır.

## Önemli Kavramlar

| Kavram | Açıklama |
|--------|----------|
| **Entity** | Kurumsal iş kuralları; uygulamadan bağımsız. |
| **Use Case / Interactor** | Uygulamaya özel iş kuralları; tek bir kullanıcı niyeti. |
| **Input/Output Port** | Use case'in giriş/çıkış arayüzleri. |
| **Presenter** | Use case çıktısını UI formatına çevirir (.NET'te genellikle atlanır, handler DTO döner). |
| **Gateway** | Dış sistem/DB arayüzü. |
| **Humble Object** | Test edilmesi zor kısmı (UI, DB) ince tutup mantığı test edilebilir tarafa çekmek. |

## Artıları

- ✅ Yüksek test edilebilirlik.
- ✅ Framework/DB'den bağımsızlık.
- ✅ Use case'ler açıkça görünür ("screaming").
- ✅ Büyük ekiplerde tutarlı yapı, hazır şablonlar.
- ✅ Uzun ömürlü projelerde bakım kolaylığı.

## Eksileri

- ❌ Yüksek başlangıç maliyeti; CRUD için "overkill".
- ❌ Bir özellik = 4 projede 6-10 dosya (hâlâ yatay dağılım).
- ❌ Gereksiz soyutlamalar (her şeye interface, repository over EF Core over DbContext).
- ❌ Mapping cehennemi (Entity ↔ DTO ↔ ViewModel).
- ❌ Yanlış anlaşıldığında "klasör yapısı" ezberine dönüşür; asıl önemli olan **bağımlılık kuralı**dır.

## Ne Zaman Kullanılır?

- Uzun ömürlü (5+ yıl), orta-büyük kurumsal uygulamalar
- Karmaşık iş kuralları + DDD
- Altyapı değişimi gerçekçi bir senaryoysa
- Büyük ekip, tutarlılık önemliyse

## Clean + VSA Hibrit (Günümüzde Popüler)

Domain ve Infrastructure ayrı projeler olarak kalır, ama `Application` içi **feature klasörlerine** göre dikey dilimlenir. Detay için bkz. [VSA](05-vertical-slice-architecture.md).
