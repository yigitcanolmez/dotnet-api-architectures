# 5. Vertical Slice Architecture (VSA)

> 💻 **Çalışan örnek proje:** [`samples/05-VerticalSlice`](../samples/05-VerticalSlice/)

> Jimmy Bogard (AutoMapper & MediatR'ın yazarı), 2018

## Neden Ortaya Çıktı?

Katmanlı, Onion ve Clean mimarilerin ortak özelliği: **kodu teknik sorumluluğa göre yatay böler.** Bogard'ın gözlemi:

- Gerçek hayattaki değişiklik talepleri **özellik bazlıdır**: "Sipariş iptaline sebep alanı ekle." Bu tek istek Controller, DTO, Service, Interface, Repository, Entity, Mapping, Validator... **6-10 dosyaya ve 4 projeye** dokunur.
- Katmanlar arasında **paylaşılan soyutlamalar** (generic repository, base service) bir özelliğin ihtiyacı için değişince **başka özellikleri bozar**.
- Her istek aynı katmanlardan geçmek zorunda; oysa "ürün listesi" basit bir SQL sorgusu, "sipariş ver" ise karmaşık domain mantığı istiyor. **Tek beden herkese uymuyor.**
- Soyutlamalar "belki lazım olur" diye önceden ekleniyor (YAGNI ihlali).

## Çözdüğü Problem

> "Değişiklik eksenine göre kodu organize et. Birlikte değişen şeyler birlikte dursun. Özellikler arası coupling'i minimize et, özellik içi cohesion'ı maksimize et."

## Temel Fikir

Her **istek (request)** bir dilimdir. Dilim, UI'dan veritabanına kadar ihtiyaç duyduğu **her şeyi** içerir.

```
          Feature:        Feature:        Feature:        Feature:
         CreateOrder    GetOrderById    CancelOrder     ListProducts
        ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐
  API   │ Endpoint │   │ Endpoint │   │ Endpoint │   │ Endpoint │
        │ Validator│   │          │   │ Validator│   │          │
  Logic │ Handler  │   │ Handler  │   │ Handler  │   │ Handler  │
        │ (Domain) │   │ (Dapper) │   │ (Domain) │   │ (EF raw) │
  Data  │ EF Core  │   │   SQL    │   │ EF Core  │   │ Cache    │
        └──────────┘   └──────────┘   └──────────┘   └──────────┘
            ▲ Her dilim kendi yaklaşımını seçebilir ▲
```

**Kurallar:**
1. Dilimler arasında coupling'i minimize et (bir dilim diğerinin handler'ını çağırmaz).
2. Dilim içinde ihtiyaç olan kadar soyutlama kullan — basitse transaction script, karmaşıksa domain model.
3. Paylaşım **ihtiyaç doğunca** yapılır (refactoring ile), önceden değil.

## .NET Klasör Yapısı

```
src/MyApp.Api/
├── Features/
│   ├── Orders/
│   │   ├── CreateOrder.cs          ← endpoint + command + validator + handler TEK DOSYADA
│   │   ├── GetOrderById.cs
│   │   ├── CancelOrder.cs
│   │   └── OrderDto.cs             ← sadece Orders feature'ları paylaşıyorsa
│   └── Products/
│       ├── ListProducts.cs
│       └── UpdatePrice.cs
├── Domain/                         ← zengin domain mantığı gerekiyorsa (opsiyonel)
│   └── Order.cs
├── Infrastructure/
│   └── AppDbContext.cs
├── Common/                         ← gerçekten ortak: behaviors, exceptions, Result
└── Program.cs
```

Çoğu zaman **tek proje** yeterlidir.

## Kod Örneği — Tek Dosyada Bir Dilim

```csharp
// Features/Orders/CreateOrder.cs
namespace MyApp.Features.Orders;

public static class CreateOrder
{
    public record Command(Guid CustomerId, List<Item> Items) : IRequest<Result<Guid>>;
    public record Item(Guid ProductId, int Quantity);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.CustomerId).NotEmpty();
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items).ChildRules(i => i.RuleFor(x => x.Quantity).GreaterThan(0));
        }
    }

    internal sealed class Handler(AppDbContext db) : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command cmd, CancellationToken ct)
        {
            var ids = cmd.Items.Select(i => i.ProductId).ToList();
            var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            if (products.Count != ids.Count) return Result.Failure<Guid>("Ürün bulunamadı");

            var order = Order.Create(cmd.CustomerId,
                cmd.Items.Select(i => (products[i.ProductId], i.Quantity)));
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);
            return order.Id;
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/orders", async (Command cmd, ISender sender) =>
        {
            var result = await sender.Send(cmd);
            return result.IsSuccess ? Results.Created($"/orders/{result.Value}", result.Value)
                                    : Results.BadRequest(result.Error);
        }).WithTags("Orders");
}
```

```csharp
// Features/Orders/GetOrderById.cs — okuma dilimi: domain yok, direkt projeksiyon
public static class GetOrderById
{
    public record Query(Guid Id) : IRequest<Response?>;
    public record Response(Guid Id, string Status, decimal Total);

    internal sealed class Handler(AppDbContext db) : IRequestHandler<Query, Response?>
    {
        public Task<Response?> Handle(Query q, CancellationToken ct) =>
            db.Orders.AsNoTracking().Where(o => o.Id == q.Id)
              .Select(o => new Response(o.Id, o.Status.ToString(), o.Total))
              .FirstOrDefaultAsync(ct);
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/orders/{id:guid}", async (Guid id, ISender s) =>
            await s.Send(new Query(id)) is { } r ? Results.Ok(r) : Results.NotFound());
}
```

### Endpoint'leri otomatik kaydetmek

```csharp
public interface IEndpoint { void MapEndpoint(IEndpointRouteBuilder app); }

// Program.cs
builder.Services.Scan(s => s.FromAssemblyOf<Program>()
    .AddClasses(c => c.AssignableTo<IEndpoint>()).AsImplementedInterfaces().WithTransientLifetime());

foreach (var ep in app.Services.GetServices<IEndpoint>()) ep.MapEndpoint(app);
```

Alternatif: **Carter**, **FastEndpoints**, **Wolverine.Http** bu kaydı hazır sunar.

## MediatR Şart mı?

Hayır. VSA bir **organizasyon prensibidir**, kütüphane değil. MediatR, pipeline behavior'lar (validation, logging, transaction) için kullanışlıdır ama:
- Doğrudan handler'ı DI ile endpoint'e enjekte edebilirsiniz.
- FastEndpoints kendi pipeline'ını sunar.
- Wolverine, Mediator (source generator) alternatiflerdir.

## VSA'da Domain Mantığı Nereye?

VSA "domain modeli kullanma" demez. Bogard'ın önerisi:
1. Önce handler içinde **transaction script** olarak yaz.
2. Kod kokusu (tekrar, karmaşık kural) belirince mantığı **domain nesnesine** taşı (push down).
3. Paylaşılan kural birden fazla dilimde gerekiyorsa domain'e veya domain service'e çıkar.

Yani VSA + DDD birlikte çok iyi çalışır.

## Artıları

- ✅ Bir özellik = bir klasör/dosya → değişiklik lokal.
- ✅ Yeni özellik eklemek mevcut kodu bozmaz (Open/Closed).
- ✅ Her dilim kendi teknik yaklaşımını seçer (EF, Dapper, raw SQL, cache).
- ✅ Daha az soyutlama, daha az mapping, daha az "boilerplate".
- ✅ CQRS ile doğal uyum (her dilim ya Command ya Query).
- ✅ Yeni geliştirici bir özelliği tek yerde okur.
- ✅ Mikroservise bölmek kolaydır (dilimler zaten bağımsız).

## Eksileri

- ❌ Kod tekrarı riski (bilinçli kabul edilir, ama kontrolsüz olursa kötü).
- ❌ Ekip disiplini ister: "Ne zaman paylaşmalı?" kararı tecrübe gerektirir.
- ❌ Domain mantığı handler'lara dağılabilir (anemic model riski).
- ❌ Çok sayıda küçük sınıf; navigasyon için iyi isimlendirme şart.
- ❌ Katman bazlı mimari kurallar (NetArchTest) daha az anlamlı.

## Clean vs VSA

| | Clean Architecture | Vertical Slice |
|---|---|---|
| Bölme ekseni | Teknik katman (yatay) | Özellik (dikey) |
| Soyutlama | Önceden, her yerde | İhtiyaç doğunca |
| Coupling | Katmanlar arası düşük, özellikler arası yüksek | Özellikler arası düşük |
| Proje sayısı | 4+ | Genellikle 1 |
| Değişiklik maliyeti | Çok dosya | Az dosya |
| En iyi olduğu yer | Karmaşık, stabil domain | Hızlı değişen, çok özellikli API |

## Ne Zaman Kullanılır?

- Çok sayıda bağımsız endpoint'i olan API'ler
- Hızlı iterasyon gereken ürünler
- CQRS kullanılan sistemler
- Mikroservislerin iç yapısı
- Modüler monolitin her modülünün iç yapısı
