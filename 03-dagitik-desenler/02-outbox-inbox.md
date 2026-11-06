# 16. Transactional Outbox & Inbox Desenleri

> 💻 **Çalışan örnek proje:** [`samples/10-Microservices`](../samples/10-Microservices/) · [`samples/08-ModularMonolith`](../samples/08-ModularMonolith/)

## Neden Ortaya Çıktı? — Dual Write Problemi

Bir servis hem **veritabanına yazmalı** hem **mesaj yayınlamalı**:

```csharp
await db.SaveChangesAsync();          // 1
await bus.Publish(new OrderPlaced()); // 2
```

Olası senaryolar:
- 1 başarılı, 2 öncesi uygulama çöktü → **Sipariş var, olay yok.** Stok hiç düşmez. 💥
- Sırayı ters çevirirsen: 2 başarılı, 1 başarısız → **Olay var, sipariş yok.** Hayalet sipariş. 💥
- DB ve broker'ı aynı transaction'a sokmak = 2PC (bkz. Saga dokümanı neden olmadığı).

Tüketici tarafında ise broker **at-least-once** teslim eder → aynı mesaj iki kez işlenebilir (ödeme iki kez çekilir 💥).

## Outbox — Çözdüğü Problem

> "Mesajı broker'a doğrudan gönderme; iş verisiyle **aynı yerel transaction'da** bir `Outbox` tablosuna yaz. Ayrı bir süreç bu tabloyu okuyup broker'a yayınlasın."

```
┌───────────── Tek DB Transaction ─────────────┐
│  INSERT INTO Orders (...)                     │
│  INSERT INTO OutboxMessages (Type, Payload)   │
└───────────────────────────────────────────────┘
                     │
         ┌───────────▼────────────┐
         │ Outbox Processor       │  (BackgroundService / polling / CDC)
         │ SELECT unprocessed     │
         │ → Publish to broker    │
         │ → Mark processed       │
         └───────────┬────────────┘
                     ▼
                  Broker
```

Garanti: **Veri kaydedildiyse mesaj eninde sonunda yayınlanır** (at-least-once).

### Manuel Uygulama

```csharp
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime OccurredOnUtc { get; set; }
    public DateTime? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
}

// Domain event'leri SaveChanges sırasında outbox'a çevir
public class ConvertDomainEventsToOutboxInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> r, CancellationToken ct = default)
    {
        var ctx = e.Context!;
        var msgs = ctx.ChangeTracker.Entries<IHasDomainEvents>()
            .SelectMany(x => { var evs = x.Entity.DomainEvents.ToList(); x.Entity.ClearEvents(); return evs; })
            .Select(ev => new OutboxMessage
            {
                Id = Guid.NewGuid(), OccurredOnUtc = DateTime.UtcNow,
                Type = ev.GetType().AssemblyQualifiedName!,
                Content = JsonSerializer.Serialize(ev, ev.GetType())
            });
        ctx.Set<OutboxMessage>().AddRange(msgs);
        return base.SavingChangesAsync(e, r, ct);
    }
}

// Processor
public class OutboxProcessor(IServiceScopeFactory sf, ILogger<OutboxProcessor> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = sf.CreateScope();
            var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bus = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            // Çoklu instance için: FOR UPDATE SKIP LOCKED (Postgres) / READPAST (SQL Server)
            var batch = await db.OutboxMessages
                .FromSqlRaw("SELECT * FROM outbox_messages WHERE processed_on_utc IS NULL ORDER BY occurred_on_utc LIMIT 50 FOR UPDATE SKIP LOCKED")
                .ToListAsync(ct);

            foreach (var m in batch)
            {
                try
                {
                    var evt = JsonSerializer.Deserialize(m.Content, Type.GetType(m.Type)!)!;
                    await bus.Publish(evt, ct);
                    m.ProcessedOnUtc = DateTime.UtcNow;
                }
                catch (Exception ex) { m.Error = ex.Message; log.LogError(ex, "Outbox {Id}", m.Id); }
            }
            await db.SaveChangesAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }
}
```

### Yayınlama Stratejileri
- **Polling Publisher**: Periyodik sorgu (yukarıdaki). Basit.
- **Transaction Log Tailing / CDC**: Debezium, SQL Server CDC ile DB log'unu okuyup Kafka'ya basmak. Düşük gecikme, polling yükü yok.

### Hazır Çözümler
- **MassTransit**: `AddEntityFrameworkOutbox<TDbContext>(o => o.UseBusOutbox())`
- **NServiceBus**: Outbox feature
- **Wolverine**: Durable outbox (Marten/EF Core)
- **CAP**: Yerleşik outbox

## Inbox — Çözdüğü Problem

> "Tüketici aynı mesajı birden fazla alabilir. İşlenen mesaj ID'lerini iş verisiyle **aynı transaction'da** kaydet; tekrar gelirse atla."

Buna **Idempotent Consumer** de denir.

```csharp
public class IdempotentConsumer<T>(AppDbContext db, IHandler<T> inner) : IConsumer<T> where T : class
{
    public async Task Consume(ConsumeContext<T> ctx)
    {
        var id = ctx.MessageId!.Value;
        var consumer = inner.GetType().Name;
        if (await db.InboxMessages.AnyAsync(x => x.MessageId == id && x.Consumer == consumer)) return;  // zaten işlendi

        await using var tx = await db.Database.BeginTransactionAsync();
        await inner.HandleAsync(ctx.Message);
        db.InboxMessages.Add(new InboxMessage { MessageId = id, Consumer = consumer, ProcessedOnUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();   // unique index (MessageId, Consumer) yarış durumunu engeller
        await tx.CommitAsync();
    }
}
```

### Idempotency Alternatifleri
- **Doğal idempotency**: `SET Status = 'Shipped'` (tekrar uygulanabilir), `+=` değil.
- **Unique constraint**: `Payments(OrderId)` unique → ikinci insert patlar, yakala ve geç.
- **Versiyon kontrolü**: `WHERE Version = @expected`.
- **API seviyesi**: HTTP `Idempotency-Key` header'ı (Stripe yaklaşımı).

## Outbox + Inbox = Effectively-Once

```
Producer DB ──(Outbox)──► Broker ──(at-least-once)──► Consumer ──(Inbox dedupe)──► Consumer DB
```

"Exactly-once delivery" dağıtık sistemlerde imkânsızdır; ama **at-least-once delivery + idempotent processing = effectively-once processing**.

## Ne Zaman Kullanılır?

- DB'ye yazıp mesaj yayınlayan **her** servis (mikroservis, modüler monolit, saga katılımcıları)
- Kritik iş olayları (ödeme, sipariş) kaybolmamalıysa
