# 1. Katmanlı Mimari (N-Tier / Layered Architecture)

> 💻 **Çalışan örnek proje:** [`samples/01-Layered`](../samples/01-Layered/)

## Neden Ortaya Çıktı?

1990'larda uygulamalar çoğunlukla **"Big Ball of Mud"** (çamur topu) şeklindeydi: SQL sorguları butonların click event'lerinde, iş kuralları HTML üreten kodun içinde, her şey birbirine bağlı. Sonuç:

- Bir değişiklik beklenmedik yerleri bozuyordu.
- Veritabanını değiştirmek = tüm uygulamayı yeniden yazmak.
- Test yazmak neredeyse imkânsızdı.
- Ekipler aynı dosyalar üzerinde çakışıyordu.

**Separation of Concerns (Sorumlulukların Ayrılması)** prensibi ile kod, teknik sorumluluğuna göre yatay katmanlara bölündü.

## Çözdüğü Problem

> "UI kodu, iş kuralı ve veri erişimi birbirine karışmasın."

## Yapı

```
┌───────────────────────────────┐
│  Presentation (API/Controller)│   HTTP, JSON, auth, routing
├───────────────────────────────┤
│  Business Logic (BLL/Service) │   İş kuralları
├───────────────────────────────┤
│  Data Access (DAL/Repository) │   SQL, EF Core
├───────────────────────────────┤
│  Database                     │
└───────────────────────────────┘
   Bağımlılık yönü: YUKARIDAN AŞAĞIYA ↓
```

**Kural:** Her katman sadece bir altındaki katmanı bilir (strict) ya da altındaki tüm katmanları bilir (relaxed).

### Tipik .NET Solution

```
MyApp.sln
├── MyApp.Api            → Controllers, Program.cs
├── MyApp.Business       → Services, DTOs
├── MyApp.DataAccess     → DbContext, Repositories, Entities
└── MyApp.Common         → Helpers, Constants
```

Referanslar: `Api → Business → DataAccess`

## Kod Örneği

```csharp
// DataAccess
public class Order { public int Id { get; set; } public decimal Total { get; set; } public string Status { get; set; } = ""; }

public class OrderRepository(AppDbContext db)
{
    public Task<Order?> GetAsync(int id) => db.Orders.FindAsync(id).AsTask();
    public Task SaveAsync() => db.SaveChangesAsync();
}

// Business
public class OrderService(OrderRepository repo)
{
    public async Task CancelAsync(int id)
    {
        var order = await repo.GetAsync(id) ?? throw new NotFoundException();
        if (order.Status == "Shipped") throw new BusinessException("Kargolanan sipariş iptal edilemez");
        order.Status = "Cancelled";
        await repo.SaveAsync();
    }
}

// Api
[ApiController, Route("orders")]
public class OrdersController(OrderService service) : ControllerBase
{
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id) { await service.CancelAsync(id); return NoContent(); }
}
```

## Temel Sorun: Bağımlılık Yönü

İş katmanı (en değerli kısım) **veri erişim katmanına bağımlıdır**. Yani:

- Entity'ler EF Core'a göre şekillenir (`virtual`, navigation property, `[Table]` attribute'ları).
- İş mantığını test etmek için veritabanına (ya da somut repository'ye) ihtiyaç duyarsınız.
- "Database-driven design" oluşur: önce tablo tasarlanır, iş mantığı ona uydurulur.

Bu sorun, **Onion / Hexagonal / Clean** mimarilerin doğmasının ana sebebidir (Dependency Inversion).

## Artıları

- ✅ Anlaşılması çok kolay, herkes bilir.
- ✅ Küçük/orta CRUD uygulamalar için hızlı.
- ✅ Ekip rolleri katmanlara bölünebilir.

## Eksileri

- ❌ İş mantığı altyapıya bağımlı (DIP ihlali).
- ❌ **Anemic Domain Model**: entity'ler sadece property çantası, mantık "Service" sınıflarında.
- ❌ Bir özellik eklemek için tüm katmanlara dokunmak gerekir (shotgun surgery).
- ❌ "God service"ler: `OrderService` 3000 satıra büyür.
- ❌ Katmanları gereksizce geçen "pass-through" kod (Controller → Service → Repository sadece çağrı iletir).

## Ne Zaman Kullanılır?

- Basit CRUD ağırlıklı iç uygulamalar, admin panelleri
- Kısa ömürlü projeler, prototipler
- Ekip daha karmaşık mimarilere hazır değilse

## Ne Zaman Kullanılmaz?

- Karmaşık iş kuralları varsa → **Clean/Onion + DDD**
- Çok sayıda bağımsız özellik varsa → **VSA**
