# 2. Onion (Soğan) Mimarisi

> 💻 **Çalışan örnek proje:** [`samples/02-Onion`](../samples/02-Onion/)

> Jeffrey Palermo, 2008

## Neden Ortaya Çıktı?

Katmanlı mimaride **iş mantığı veri erişimine bağımlıydı**. Bu şu demekti:

- Veritabanı/ORM değişirse iş kuralları da değişmek zorunda kalıyordu.
- İş mantığını izole test etmek zordu.
- Uygulamanın kalbi (domain) en kırılgan parçaya (altyapı) bağlıydı.

Palermo şu soruyu sordu: *"Uygulamanın en kalıcı ve değerli parçası nedir?"* Cevap: **Domain (iş modeli)**. Framework'ler, veritabanları, UI'lar değişir; "sipariş kargolandıktan sonra iptal edilemez" kuralı değişmez.

## Çözdüğü Problem

> "İş mantığı altyapı detaylarına (DB, ORM, e-posta, dosya sistemi) bağımlı olmasın; altyapı iş mantığına bağımlı olsun."

Bunu **Dependency Inversion Principle (DIP)** ile yapar: Arayüzler (interface) içeride tanımlanır, implementasyonlar dışarıda.

## Yapı

```
           ┌─────────────────────────────────────────┐
           │  Infrastructure / UI / Tests            │  ← en dış
           │   ┌─────────────────────────────────┐   │
           │   │  Application Services           │   │
           │   │   ┌─────────────────────────┐   │   │
           │   │   │  Domain Services        │   │   │
           │   │   │   ┌─────────────────┐   │   │   │
           │   │   │   │  Domain Model   │   │   │   │  ← merkez
           │   │   │   └─────────────────┘   │   │   │
           │   │   └─────────────────────────┘   │   │
           │   └─────────────────────────────────┘   │
           └─────────────────────────────────────────┘
           Bağımlılıklar daima MERKEZE doğru →●
```

### Katmanlar

| Katman | İçerik | Neye bağımlı? |
|--------|--------|---------------|
| **Domain Model** | Entity, Value Object, domain kuralları | Hiçbir şeye |
| **Domain Services** | Birden fazla entity içeren kurallar, repository **arayüzleri** | Domain Model |
| **Application Services** | Use case orkestrasyonu, DTO, `IEmailSender` gibi arayüzler | Domain |
| **Infrastructure / UI** | EF Core, SMTP, Controller'lar, testler | İç katmanlar |

### Temel Kurallar (Palermo)

1. Uygulama bağımsız bir obje modeli etrafında kurulur.
2. İç katmanlar arayüz tanımlar, dış katmanlar implemente eder.
3. Bağımlılık yönü merkeze doğrudur.
4. Tüm uygulama çekirdeği altyapıdan bağımsız derlenip çalıştırılabilir.

## .NET Solution Yapısı

```
src/
├── MyApp.Domain              (referans: YOK)
│   ├── Entities/Order.cs
│   ├── ValueObjects/Money.cs
│   └── Interfaces/IOrderRepository.cs
├── MyApp.Application         (referans: Domain)
│   ├── Interfaces/IEmailSender.cs
│   ├── Orders/OrderAppService.cs
│   └── DTOs/
├── MyApp.Infrastructure      (referans: Application, Domain)
│   ├── Persistence/AppDbContext.cs
│   ├── Persistence/OrderRepository.cs
│   └── Email/SmtpEmailSender.cs
└── MyApp.Api                 (referans: Application, Infrastructure — sadece DI composition için)
    └── Program.cs
```

## Kod Örneği

```csharp
// ---------- Domain (saf C#, NuGet yok) ----------
public class Order
{
    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyList<OrderLine> Lines => _lines;

    public void Cancel()
    {
        if (Status == OrderStatus.Shipped)
            throw new DomainException("Kargolanan sipariş iptal edilemez.");
        Status = OrderStatus.Cancelled;
    }
}

public interface IOrderRepository   // arayüz İÇERİDE
{
    Task<Order?> GetAsync(Guid id, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

// ---------- Application ----------
public interface INotificationService { Task NotifyCancelledAsync(Guid orderId); }

public class OrderAppService(IOrderRepository repo, INotificationService notifier)
{
    public async Task CancelAsync(Guid id, CancellationToken ct)
    {
        var order = await repo.GetAsync(id, ct) ?? throw new NotFoundException();
        order.Cancel();                       // kural domain'de
        await repo.SaveChangesAsync(ct);
        await notifier.NotifyCancelledAsync(id);
    }
}

// ---------- Infrastructure (implementasyon DIŞARIDA) ----------
public class EfOrderRepository(AppDbContext db) : IOrderRepository
{
    public Task<Order?> GetAsync(Guid id, CancellationToken ct) =>
        db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

// ---------- Api / Composition Root ----------
builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<INotificationService, EmailNotificationService>();
```

### EF Core konfigürasyonu Domain'i kirletmemeli

```csharp
// Infrastructure/Persistence/Configurations/OrderConfiguration.cs
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.HasKey(x => x.Id);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

## Mimari Kuralları Test Etmek

```csharp
[Fact]
public void Domain_should_not_depend_on_other_layers()
{
    var result = Types.InAssembly(typeof(Order).Assembly)
        .ShouldNot().HaveDependencyOnAny("MyApp.Application", "MyApp.Infrastructure", "Microsoft.EntityFrameworkCore")
        .GetResult();
    Assert.True(result.IsSuccessful);
}
```

## Artıları

- ✅ Domain tamamen test edilebilir (mock'suz unit test).
- ✅ Altyapı değişimi (SQL → Mongo, SMTP → SendGrid) domain'i etkilemez.
- ✅ Rich Domain Model'i teşvik eder.
- ✅ DDD ile çok iyi uyum.

## Eksileri

- ❌ Basit CRUD için fazla tören (interface + impl + DTO + mapping).
- ❌ Çok sayıda proje/dosya.
- ❌ Yine de **yatay** katmanlar: bir özellik 4 projeye dağılır.
- ❌ "Her şey için interface" aşırılığına kaçılabilir.

## Onion vs Katmanlı — Tek Fark

| | Katmanlı | Onion |
|---|---|---|
| Repository interface nerede? | DataAccess | Domain/Application |
| İş katmanı neye bağımlı? | DataAccess'e | Hiçbir şeye |
| Merkezde ne var? | Veritabanı | Domain |

## Ne Zaman Kullanılır?

- Uzun ömürlü, iş kuralları zengin uygulamalar
- DDD uygulanacaksa
- Altyapı bağımsızlığı gerçekten önemliyse
