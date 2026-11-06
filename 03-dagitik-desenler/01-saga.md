# 15. Saga Deseni

> 💻 **Çalışan örnek proje:** [`samples/09-SagaChoreography`](../samples/09-SagaChoreography/) · [`samples/10-Microservices`](../samples/10-Microservices/)

> Hector Garcia-Molina & Kenneth Salem, "Sagas", 1987 — mikroservislerle yeniden popülerleşti (Chris Richardson)

## Neden Ortaya Çıktı?

Monolitte bir sipariş işlemi tek bir **ACID transaction** idi:

```sql
BEGIN TRAN
  INSERT INTO Orders ...
  UPDATE Stock SET Qty = Qty - 1 ...
  INSERT INTO Payments ...
COMMIT   -- ya hepsi ya hiçbiri
```

Mikroservislerde (database-per-service) bu üç tablo **üç farklı servisin üç farklı veritabanında**. Artık:

- Tek bir `BEGIN TRAN` yok.
- **2PC (Two-Phase Commit / Distributed Transaction / MSDTC)** teorik çözüm, ama:
  - Tüm katılımcıları kilitler → **düşük throughput**, ölçeklenmez.
  - Koordinatör çökerse kaynaklar kilitli kalır (blocking protocol).
  - Çoğu modern broker ve NoSQL DB (RabbitMQ, Kafka, Mongo, Cosmos) **desteklemez**.
  - Availability'yi düşürür (tüm katılımcılar aynı anda ayakta olmalı).
  - .NET Core'da MSDTC desteği uzun süre yoktu (yalnızca Windows'ta, .NET 7+ ile kısıtlı geldi).

## Çözdüğü Problem

> "Birden fazla servisi kapsayan bir iş işlemini, her biri kendi yerel ACID transaction'ı olan **adımlar dizisine** böl. Bir adım başarısız olursa, önceki adımların etkisini **telafi eden (compensating) işlemleri** ters sırada çalıştır."

Sonuç: Atomiklik yerine **eventual consistency** + **semantik geri alma**.

```
Mutlu yol:
  T1: CreateOrder(Pending) → T2: ReserveStock → T3: ChargePayment → T4: ApproveOrder ✅

T3 başarısız olursa:
  T1 → T2 → T3 ❌ → C2: ReleaseStock → C1: RejectOrder   (telafi, ters sırada)
```

## Temel Kavramlar

| Kavram | Açıklama |
|--------|----------|
| **Local Transaction (Tᵢ)** | Tek servisin kendi DB'sinde yaptığı ACID işlem. |
| **Compensating Transaction (Cᵢ)** | Tᵢ'nin **semantik** tersi. "Sil" değil, "iptal et/iade et". Rollback değildir! |
| **Compensatable Transaction** | Geri alınabilen adım (stok rezervasyonu). |
| **Pivot Transaction** | Geri dönüşü olmayan nokta. Başarılı olursa saga sonuna kadar gider (ör. ödeme çekildi). |
| **Retriable Transaction** | Pivot'tan sonraki adımlar; başarılı olana kadar **tekrar denenir**, telafi edilmez (ör. e-posta gönder). |

> **Tasarım ipucu:** Adımları sıralarken önce compensatable, sonra pivot, en son retriable adımları koyun.

## İki Uygulama Stili

---

### A) Choreography (Koreografi) — Merkezi koordinatör YOK

Her servis olayları dinler, kendi işini yapar ve yeni bir olay yayınlar.

```
 Ordering            Inventory             Payment              Ordering
    │                    │                    │                    │
    │─ OrderCreated ────►│                    │                    │
    │                    │─ StockReserved ───►│                    │
    │                    │                    │─ PaymentCompleted ►│ → Order Approved ✅
    │                    │                    │                    │
  Hata durumu:           │                    │                    │
    │                    │                    │─ PaymentFailed ───►│ → Order Rejected
    │                    │◄────────────── PaymentFailed ───────────│
    │                    │ (ReleaseStock)     │                    │
```

```csharp
// ---- Inventory servisi ----
public class OrderCreatedConsumer(InventoryDbContext db, IPublishEndpoint bus) : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> ctx)
    {
        var ok = await TryReserveAsync(ctx.Message);
        if (ok) await bus.Publish(new StockReserved(ctx.Message.OrderId, ctx.Message.Total));
        else    await bus.Publish(new StockReservationFailed(ctx.Message.OrderId, "Stok yok"));
        await db.SaveChangesAsync();
    }
}

public class PaymentFailedConsumer(InventoryDbContext db) : IConsumer<PaymentFailed>
{
    public async Task Consume(ConsumeContext<PaymentFailed> ctx)
    {
        // Compensation
        var reservation = await db.Reservations.SingleAsync(r => r.OrderId == ctx.Message.OrderId);
        reservation.Release();
        await db.SaveChangesAsync();
    }
}

// ---- Payment servisi ----
public class StockReservedConsumer(IPaymentGateway pg, IPublishEndpoint bus) : IConsumer<StockReserved>
{
    public async Task Consume(ConsumeContext<StockReserved> ctx)
    {
        var r = await pg.ChargeAsync(ctx.Message.OrderId, ctx.Message.Amount);
        await (r.Success
            ? bus.Publish(new PaymentCompleted(ctx.Message.OrderId))
            : bus.Publish(new PaymentFailed(ctx.Message.OrderId, r.Error)));
    }
}

// ---- Ordering servisi ----
public class OrderSagaEventsConsumer(OrderingDbContext db) :
    IConsumer<PaymentCompleted>, IConsumer<PaymentFailed>, IConsumer<StockReservationFailed>
{
    public async Task Consume(ConsumeContext<PaymentCompleted> c)       => await Update(c.Message.OrderId, o => o.Approve());
    public async Task Consume(ConsumeContext<PaymentFailed> c)          => await Update(c.Message.OrderId, o => o.Reject(c.Message.Reason));
    public async Task Consume(ConsumeContext<StockReservationFailed> c) => await Update(c.Message.OrderId, o => o.Reject(c.Message.Reason));
    private async Task Update(Guid id, Action<Order> a) { var o = await db.Orders.FindAsync(id); a(o!); await db.SaveChangesAsync(); }
}
```

**Artıları**
- ✅ Basit; az sayıda adımda çok hızlı uygulanır.
- ✅ Merkezi tek hata noktası yok.
- ✅ Maksimum gevşek bağlılık.

**Eksileri**
- ❌ Akış hiçbir yerde **yazılı değil**; kodu okuyarak süreci anlamak zor.
- ❌ Adım sayısı arttıkça (4+) "olay spagettisi".
- ❌ Döngüsel bağımlılık riski (Inventory, Payment'ın event'ini; Payment, Inventory'nin event'ini dinler).
- ❌ "Sipariş şu an hangi adımda?" sorusunun cevabı dağınık.
- ❌ Test etmek zor.

---

### B) Orchestration (Orkestrasyon) — Merkezi koordinatör VAR

Bir **orkestratör** (saga state machine) süreci yönetir: hangi adımın çalışacağına karar verir, servislere **komut** gönderir, yanıtları (event) dinler, durumu kalıcı olarak saklar.

```
                        ┌──────────────────────────┐
                        │   Order Saga Orchestrator │
                        │   State: AwaitingPayment  │  ← DB'de saklanır
                        └───┬─────────▲────────┬────┘
           ReserveStock(cmd)│         │        │ChargePayment(cmd)
                            ▼         │events  ▼
                       ┌─────────┐    │   ┌─────────┐
                       │Inventory│────┴───│ Payment │
                       └─────────┘        └─────────┘
```

#### MassTransit State Machine ile

```csharp
// Saga durumu (DB'de saklanır)
public class OrderState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }          // = OrderId
    public string CurrentState { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public decimal Total { get; set; }
    public string? FailureReason { get; set; }
    public Guid? PaymentTimeoutTokenId { get; set; }
    public int Version { get; set; }
}

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public State AwaitingStock { get; private set; } = null!;
    public State AwaitingPayment { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
    public Event<StockReserved> StockReserved { get; private set; } = null!;
    public Event<StockReservationFailed> StockFailed { get; private set; } = null!;
    public Event<PaymentCompleted> PaymentCompleted { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailed { get; private set; } = null!;
    public Schedule<OrderState, PaymentTimeout> PaymentTimeout { get; private set; } = null!;

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderSubmitted,   e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => StockReserved,    e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => StockFailed,      e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentCompleted, e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentFailed,    e => e.CorrelateById(m => m.Message.OrderId));
        Schedule(() => PaymentTimeout, x => x.PaymentTimeoutTokenId, s => s.Delay = TimeSpan.FromMinutes(10));

        Initially(
            When(OrderSubmitted)
                .Then(c => { c.Saga.CustomerId = c.Message.CustomerId; c.Saga.Total = c.Message.Total; })
                .Send(new Uri("queue:reserve-stock"), c => new ReserveStock(c.Saga.CorrelationId, c.Message.Items))
                .TransitionTo(AwaitingStock));

        During(AwaitingStock,
            When(StockReserved)
                .Send(new Uri("queue:charge-payment"), c => new ChargePayment(c.Saga.CorrelationId, c.Saga.CustomerId, c.Saga.Total))
                .Schedule(PaymentTimeout, c => new PaymentTimeout(c.Saga.CorrelationId))
                .TransitionTo(AwaitingPayment),
            When(StockFailed)
                .Then(c => c.Saga.FailureReason = c.Message.Reason)
                .Publish(c => new OrderRejected(c.Saga.CorrelationId, c.Message.Reason))
                .TransitionTo(Failed));

        During(AwaitingPayment,
            When(PaymentCompleted)
                .Unschedule(PaymentTimeout)
                .Publish(c => new OrderApproved(c.Saga.CorrelationId))
                .TransitionTo(Completed),
            When(PaymentFailed)
                .Unschedule(PaymentTimeout)
                .Send(new Uri("queue:release-stock"), c => new ReleaseStock(c.Saga.CorrelationId))   // compensation
                .Publish(c => new OrderRejected(c.Saga.CorrelationId, c.Message.Reason))
                .TransitionTo(Failed),
            When(PaymentTimeout.Received)
                .Send(new Uri("queue:release-stock"), c => new ReleaseStock(c.Saga.CorrelationId))
                .Publish(c => new OrderRejected(c.Saga.CorrelationId, "Ödeme zaman aşımı"))
                .TransitionTo(Failed));

        SetCompletedWhenFinalized();
    }
}

// Kayıt
builder.Services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
     .EntityFrameworkRepository(r => { r.ConcurrencyMode = ConcurrencyMode.Optimistic; r.ExistingDbContext<SagaDbContext>(); r.UsePostgres(); });
    x.AddEntityFrameworkOutbox<SagaDbContext>(o => { o.UsePostgres(); o.UseBusOutbox(); });
    x.AddDelayedMessageScheduler();
    x.UsingRabbitMq((ctx, cfg) => { cfg.UseDelayedMessageScheduler(); cfg.ConfigureEndpoints(ctx); });
});
```

**Artıları**
- ✅ İş akışı **tek yerde**, okunabilir, durum makinesi olarak açık.
- ✅ Saga'nın anlık durumu sorgulanabilir ("sipariş ödeme bekliyor").
- ✅ Timeout, retry, compensation merkezi yönetilir.
- ✅ Katılımcı servisler basit kalır (sadece komut al, olay yayınla); birbirini tanımaz.
- ✅ Karmaşık akışlar ve test için daha uygun.

**Eksileri**
- ❌ Orkestratör ek bir bileşen; iş mantığının oraya yığılma riski ("god orchestrator").
- ❌ Orkestratör ile katılımcılar arasında coupling.
- ❌ Saga state'i için kalıcılık ve concurrency yönetimi.

---

## Choreography vs Orchestration

| Kriter | Choreography | Orchestration |
|--------|--------------|---------------|
| Koordinasyon | Dağıtık (olaylar) | Merkezi (orkestratör) |
| Mesaj tipi | Event | Command + Event |
| Akışın görünürlüğü | Düşük | Yüksek |
| Coupling | Düşük | Orta |
| Adım sayısı | 2–4 için ideal | 4+ veya karmaşık dallanma |
| Durum takibi | Zor | Kolay |
| Timeout / zamanlayıcı | Zor | Kolay |
| Tek hata noktası | Yok | Orkestratör (ama kalıcı ve ölçeklenebilir) |

## Saga'nın Zor Kısımları: İzolasyon Eksikliği

Saga'lar ACID'in **"I" (Isolation)** harfini kaybeder. Ara durumlar diğer işlemler tarafından **görülebilir**. Anomaliler:

- **Lost update**: Bir saga, başka saga'nın değişikliğini ezer.
- **Dirty read**: Henüz tamamlanmamış (belki telafi edilecek) bir veriyi okumak. Ör. rezerve edilmiş ama ödenmemiş stok "satıldı" görünür.
- **Fuzzy/non-repeatable read**: Saga'nın iki adımı aynı veriyi farklı görür.

### Karşı Önlemler (Countermeasures)

| Önlem | Açıklama |
|-------|----------|
| **Semantic Lock** | Kayda "işlemde" bayrağı koy: `Order.Status = PENDING`. Diğer işlemler buna göre davranır. |
| **Commutative Updates** | Sıradan bağımsız işlemler tasarla (`Balance += x`). |
| **Pessimistic View** | Adımları dirty read riskini azaltacak sırada diz. |
| **Reread Value** | Güncellemeden önce değeri tekrar oku, değiştiyse iptal et (optimistic). |
| **Version File** | İşlemleri kaydet, sıra dışı gelenleri yeniden sırala. |
| **By Value** | İşlem riskine göre saga veya dağıtık transaction seç. |

## Saga'yı Güvenilir Kılmak İçin Şartlar

1. **Idempotency**: Her adım ve telafi birden çok kez çalışabilmeli (at-least-once delivery). → **Inbox**
2. **Atomik yayın**: Yerel DB değişikliği + mesaj gönderimi atomik olmalı. → **Outbox**
3. **Telafiler başarısız olamaz** (olmamalı): Retry edilebilir tasarlanmalı; son çare insan müdahalesi (manual intervention queue).
4. **Correlation ID**: Tüm mesajlarda saga kimliği.
5. **Timeout**: Yanıt gelmeyen adımlar için zamanlayıcı.
6. **Gözlemlenebilirlik**: Distributed tracing, saga durum paneli.

## .NET'te Saga Araçları

| Araç | Stil | Not |
|------|------|-----|
| **MassTransit** State Machine (Automatonymous) | Orchestration | En yaygın açık kaynak seçenek (v9+ ticari lisans) |
| **NServiceBus** Saga | Orchestration | Ticari, olgun, güçlü araç seti |
| **Wolverine** Saga | Orchestration | Marten ile entegre, modern |
| **Rebus** Saga | Orchestration | Hafif |
| **Temporal .NET SDK** | Workflow-as-code | Kod gibi yazılan dayanıklı iş akışları |
| **Dapr Workflow** | Workflow-as-code | Platform bağımsız |
| **Azure Durable Functions** | Workflow-as-code | Serverless |
| **CAP (DotNetCore.CAP)** | Choreography + Outbox | Hafif, popüler |

### Workflow-as-code örneği (Temporal)

```csharp
[Workflow]
public class OrderWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(OrderInput o)
    {
        var compensations = new Stack<Func<Task>>();
        var opts = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) };
        try
        {
            await Workflow.ExecuteActivityAsync((Activities a) => a.ReserveStock(o), opts);
            compensations.Push(() => Workflow.ExecuteActivityAsync((Activities a) => a.ReleaseStock(o), opts));

            await Workflow.ExecuteActivityAsync((Activities a) => a.ChargePayment(o), opts);
            compensations.Push(() => Workflow.ExecuteActivityAsync((Activities a) => a.Refund(o), opts));

            await Workflow.ExecuteActivityAsync((Activities a) => a.Ship(o), opts);
        }
        catch
        {
            while (compensations.Count > 0) await compensations.Pop()();
            throw;
        }
    }
}
```

## Ne Zaman Kullanılır?

- Database-per-service mimaride birden fazla servisi kapsayan iş süreçleri
- Uzun süren işlemler (dakikalar, günler — ör. onay bekleyen kredi başvurusu)
- Modüler monolitte modüller arası, ayrı DbContext'li süreçler

## Ne Zaman Kullanılmaz?

- İşlem tek bir servis/DB içinde kalıyorsa → normal transaction.
- Sürekli saga ihtiyacı doğuyorsa → **servis sınırları yanlış çizilmiş olabilir**. Birlikte tutarlı olması gereken veriler aynı bounded context'te olmalı.
