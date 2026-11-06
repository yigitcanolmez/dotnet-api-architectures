# 10. Minimal API ve REPR (Request–Endpoint–Response) Deseni

> 💻 **Çalışan örnek proje:** [`samples/05-VerticalSlice`](../samples/05-VerticalSlice/)

> Minimal API: .NET 6 (2021) · REPR: Steve "Ardalis" Smith, 2021 · FastEndpoints

## Neden Ortaya Çıktı?

Klasik MVC Controller yaklaşımının sorunları:

- **Şişman Controller'lar**: `OrdersController` 20 action, 15 bağımlılık enjekte eder; her action sadece 2-3'ünü kullanır.
- Controller aslında bir **"klasör"** gibi davranıyor; action'lar birbirinden bağımsız ama aynı sınıfta.
- MVC altyapısı (model binding, filters, action selection) API için gereğinden ağır.
- Node.js/Express, Go, Python FastAPI gibi ekosistemlerde 3 satırda endpoint yazılırken .NET'te tören fazlaydı.
- Cloud-native/serverless'ta **startup süresi** ve **bellek** önem kazandı; AOT (Native AOT) desteği için reflection azaltılmalıydı.

## Çözdüğü Problem

> "Her endpoint kendi başına bir birimdir; sadece ihtiyaç duyduğu bağımlılıkları alsın, kendi request ve response tipine sahip olsun."

## Minimal API

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>();
var app = builder.Build();

var orders = app.MapGroup("/orders").WithTags("Orders").RequireAuthorization();

orders.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
    await db.Orders.FindAsync(id) is { } o ? TypedResults.Ok(o) : Results.NotFound());

orders.MapPost("/", async (CreateOrderRequest req, IValidator<CreateOrderRequest> v, AppDbContext db) =>
{
    var vr = await v.ValidateAsync(req);
    if (!vr.IsValid) return Results.ValidationProblem(vr.ToDictionary());
    // ...
    return Results.Created($"/orders/{id}", id);
}).AddEndpointFilter<LoggingFilter>();

app.Run();
```

Özellikler: `MapGroup`, `TypedResults`, endpoint filter'lar, `[AsParameters]`, OpenAPI entegrasyonu, Native AOT uyumu.

## REPR Deseni

Her endpoint **3 parçadan** oluşur:
- **Request**: Giriş DTO'su
- **Endpoint**: Tek bir handler sınıfı
- **Response**: Çıkış DTO'su

### FastEndpoints ile

```csharp
public class CreateOrderRequest  { public Guid CustomerId { get; set; } public List<Item> Items { get; set; } = []; }
public class CreateOrderResponse { public Guid OrderId { get; set; } }

public class CreateOrderValidator : Validator<CreateOrderRequest>
{
    public CreateOrderValidator() => RuleFor(x => x.Items).NotEmpty();
}

public class CreateOrderEndpoint(AppDbContext db) : Endpoint<CreateOrderRequest, CreateOrderResponse>
{
    public override void Configure()
    {
        Post("/orders");
        Roles("Customer");
        Summary(s => s.Summary = "Yeni sipariş oluşturur");
    }

    public override async Task HandleAsync(CreateOrderRequest req, CancellationToken ct)
    {
        var order = Order.Create(req.CustomerId, req.Items);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        await SendCreatedAtAsync<GetOrderEndpoint>(new { id = order.Id }, new CreateOrderResponse { OrderId = order.Id }, cancellation: ct);
    }
}
```

## İlişkisi

- REPR, **VSA'nın endpoint seviyesindeki** doğal ifadesidir.
- REPR ile çoğu zaman MediatR'a gerek kalmaz (endpoint = handler).
- Clean Architecture'da Web katmanını incelterek kullanılabilir.

## Artıları

- ✅ Düşük tören, hızlı geliştirme.
- ✅ Endpoint başına minimal bağımlılık.
- ✅ Daha hızlı startup, daha az bellek, Native AOT.
- ✅ Bir endpoint = bir dosya → kolay navigasyon.

## Eksileri

- ❌ `Program.cs` disiplin olmadan devasa büyüyebilir (extension method'larla gruplanmalı).
- ❌ MVC'nin bazı özellikleri (ör. bazı filter türleri, view'lar) yok/farklı.
- ❌ Çok sayıda küçük sınıf.

## Ne Zaman Kullanılır?

- Yeni .NET 8+ API projelerinin çoğunda varsayılan seçim
- Mikroservisler, serverless, yüksek performans gereken API'ler
- VSA ile birlikte
