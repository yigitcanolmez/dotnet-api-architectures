# 7. CQRS (Command Query Responsibility Segregation)

> 💻 **Çalışan örnek proje:** [`samples/06-CQRS`](../samples/06-CQRS/)

> Greg Young, ~2010 (Bertrand Meyer'in CQS prensibinden türetildi)

## Neden Ortaya Çıktı?

Geleneksel yaklaşımda **tek bir model** hem okuma hem yazma için kullanılır. Sorunlar:

- **Yazma** tarafı: iş kuralları, validasyon, tutarlılık, aggregate'ler ister.
- **Okuma** tarafı: hızlı, join'li, denormalize, ekran-özel veri ister (DTO, sayfalama, filtreleme).
- Tek model ikisine de hizmet etmeye çalışınca:
  - Domain entity'leri ekran ihtiyaçları için şişer (`CustomerNameForDisplay` vb.).
  - Okuma sorguları aggregate'leri tamamen yükler → **performans** sorunu.
  - Okuma ve yazma yükü genelde çok asimetriktir (ör. %95 okuma) ama **ayrı ölçeklenemez**.
  - Karmaşık raporlama sorguları ORM ile zorlanır.

## Çözdüğü Problem

> "Okuma ve yazma farklı ihtiyaçlara sahiptir; onlar için farklı modeller kullan."

**CQS (Meyer)**: Bir metot ya durumu değiştirir (command) ya veri döner (query), ikisini birden yapmaz.
**CQRS (Young)**: Bu prensibi **nesne/model/sistem** seviyesine taşır.

## Seviyeler (CQRS bir spektrumdur)

### Seviye 1 — Aynı DB, ayrı kod yolları (en yaygın)
```
          ┌── Command ──► Handler ──► Domain Model ──► EF Core ──┐
API ──────┤                                                      ├──► Tek DB
          └── Query ────► Handler ──► Dapper/EF Projection ──────┘
```

### Seviye 2 — Ayrı okuma modeli (view/tablo), aynı DB
Yazma normalize tablolara, okuma materialized view veya denormalize tablolara.

### Seviye 3 — Ayrı veritabanları
```
Command ─► Write DB (SQL Server) ─► Event ─► Projector ─► Read DB (Elastic/Redis/Mongo) ◄─ Query
```
Okuma tarafı **eventually consistent**'tır.

### Seviye 4 — CQRS + Event Sourcing
Yazma tarafı event store'dur; okuma modelleri event'lerden projekte edilir. Bkz. [Event Sourcing](08-event-sourcing.md).

## .NET Kod Örneği (Seviye 1)

```csharp
// ---------- Command ----------
public record ShipOrderCommand(Guid OrderId, string TrackingNo) : IRequest;

public class ShipOrderHandler(AppDbContext db) : IRequestHandler<ShipOrderCommand>
{
    public async Task Handle(ShipOrderCommand c, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([c.OrderId], ct) ?? throw new NotFoundException();
        order.Ship(c.TrackingNo);            // domain kuralları
        await db.SaveChangesAsync(ct);
    }
}

// ---------- Query (domain'i bypass eder) ----------
public record GetCustomerOrdersQuery(Guid CustomerId, int Page) : IRequest<IReadOnlyList<OrderSummary>>;
public record OrderSummary(Guid Id, DateTime Date, decimal Total, string Status, int ItemCount);

public class GetCustomerOrdersHandler(IDbConnection conn) : IRequestHandler<GetCustomerOrdersQuery, IReadOnlyList<OrderSummary>>
{
    public async Task<IReadOnlyList<OrderSummary>> Handle(GetCustomerOrdersQuery q, CancellationToken ct)
    {
        const string sql = """
            SELECT o.Id, o.CreatedAt AS Date, o.Total, o.Status, COUNT(l.Id) AS ItemCount
            FROM Orders o LEFT JOIN OrderLines l ON l.OrderId = o.Id
            WHERE o.CustomerId = @CustomerId
            GROUP BY o.Id, o.CreatedAt, o.Total, o.Status
            ORDER BY o.CreatedAt DESC
            OFFSET @Skip ROWS FETCH NEXT 20 ROWS ONLY
            """;
        var rows = await conn.QueryAsync<OrderSummary>(sql, new { q.CustomerId, Skip = q.Page * 20 });
        return rows.AsList();
    }
}
```

### Seviye 3 — Projeksiyon örneği

```csharp
public class OrderSummaryProjector(IMongoCollection<OrderSummaryDoc> col) :
    IConsumer<OrderPlaced>, IConsumer<OrderShipped>
{
    public Task Consume(ConsumeContext<OrderPlaced> ctx) =>
        col.InsertOneAsync(new OrderSummaryDoc(ctx.Message.OrderId, ctx.Message.Total, "Placed"));

    public Task Consume(ConsumeContext<OrderShipped> ctx) =>
        col.UpdateOneAsync(x => x.Id == ctx.Message.OrderId,
            Builders<OrderSummaryDoc>.Update.Set(x => x.Status, "Shipped"));
}
```

## Önemli Noktalar

- **Command'lar** genelde veri döndürmez (veya sadece ID/Result döner).
- **Query'ler** asla durum değiştirmez → güvenle cache'lenebilir, retry edilebilir.
- Command isimleri **niyet** belirtir: `UpdateOrder` değil `ShipOrder`, `CancelOrder`.
- Okuma tarafı eventually consistent ise UI bunu yönetmeli (optimistic UI, "işleminiz alındı" mesajı, polling, SignalR).

## Artıları

- ✅ Okuma ve yazma bağımsız optimize ve ölçeklenir.
- ✅ Domain modeli ekran ihtiyaçlarından arınır.
- ✅ Basit, hızlı, ekran-özel sorgular.
- ✅ Görev odaklı (task-based) UI'ya doğal uyum.
- ✅ VSA ile mükemmel uyum (her dilim bir command veya query).

## Eksileri

- ❌ Seviye 3+'da **eventual consistency** karmaşıklığı.
- ❌ Senkronizasyon altyapısı (broker, projector, yeniden oluşturma).
- ❌ Basit CRUD için gereksiz.
- ❌ Daha çok sınıf.

## Yaygın Yanılgılar

- ❌ "CQRS = MediatR" → Hayır. MediatR sadece bir in-process mesajlaşma aracı.
- ❌ "CQRS = iki veritabanı" → Hayır. Seviye 1 de CQRS'tir.
- ❌ "CQRS Event Sourcing gerektirir" → Hayır, ama ES, CQRS gerektirir.

## Ne Zaman Kullanılır?

- Okuma/yazma yükü asimetrik ise
- Karmaşık domain + karmaşık raporlama varsa
- Seviye 1 hemen hemen her orta ölçekli API'de faydalıdır
- Seviye 3+ sadece gerçek ölçek/performans ihtiyacında
