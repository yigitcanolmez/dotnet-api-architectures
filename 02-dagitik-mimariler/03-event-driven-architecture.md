# 13. Event-Driven Architecture (EDA)

> 💻 **Çalışan örnek proje:** [`samples/10-Microservices`](../samples/10-Microservices/) · [`samples/09-SagaChoreography`](../samples/09-SagaChoreography/) · [`samples/08-ModularMonolith`](../samples/08-ModularMonolith/)

## Neden Ortaya Çıktı?

Servisler birbirini **doğrudan çağırdığında** (request/response):

- **Temporal coupling**: Sipariş servisi, e-posta servisi ayakta değilse sipariş alamaz.
- **Bilgi coupling'i**: Sipariş servisi; stok, fatura, e-posta, analitik, sadakat puanı servislerini **tanımak** zorunda. Yeni bir tüketici eklemek = sipariş servisini değiştirmek.
- **Kademeli hatalar**: Bir servis yavaşlayınca tüm zincir yavaşlar.
- **Ölçek**: Ani yük patlamalarını (Black Friday) senkron zincir kaldıramaz.

## Çözdüğü Problem

> "Servisler birbirine ne yapacağını söylemesin; **ne olduğunu** duyursun. İlgilenen dinlesin."

Üretici (producer), tüketicileri (consumer) **bilmez**. Bu, en güçlü gevşek bağlılık biçimidir.

## Yapı

```
                              ┌──────────────► Inventory  (stok düş)
                              │
 Ordering ── OrderPlaced ──► [ Broker ] ──────► Billing    (fatura kes)
                              │
                              ├──────────────► Notification (e-posta)
                              │
                              └──────────────► Analytics  (yeni eklendi — Ordering değişmedi!)
```

## Mesaj Türleri — Farkı Bilmek Kritik

| Tür | Anlam | Zaman | Alıcı | Örnek |
|-----|-------|-------|-------|-------|
| **Command** | "Bunu yap" (niyet) | Emir kipi | **Tek** alıcı | `ChargePayment` |
| **Event** | "Bu oldu" (olgu) | Geçmiş zaman | **0..N** alıcı | `PaymentCharged` |
| **Query** | "Bana bunu ver" | — | Tek | `GetOrderStatus` |

### Event Türleri

- **Domain Event**: Bounded context **içinde**, in-process (MediatR notification). Zengin, iç model içerebilir.
- **Integration Event**: Bounded context'ler **arası**, broker üzerinden. Public sözleşme; versiyonlanmalı, minimal tutulmalı.

### Event İçeriği Stratejileri

| Strateji | İçerik | Artı | Eksi |
|----------|--------|------|------|
| **Event Notification** | Sadece ID: `OrderPlaced { OrderId }` | Küçük, gizlilik | Tüketici geri çağırmak zorunda (coupling) |
| **Event-Carried State Transfer** | Tüm veri: `OrderPlaced { OrderId, Items, Total, Address }` | Tüketici otonom | Büyük mesaj, şema bağımlılığı |
| **Event Sourcing** | Event = kalıcı kaynak | Bkz. ES dokümanı | |

## Topolojiler

### Broker Topology (Choreography)
Merkezi koordinatör yok; her servis olaylara tepki verir. → Bkz. [Saga Choreography](../03-dagitik-desenler/01-saga.md)

### Mediator Topology (Orchestration)
Bir orkestratör olayları dinler ve sıradaki adımı komutla tetikler. → Bkz. [Saga Orchestration](../03-dagitik-desenler/01-saga.md)

## Broker Seçimi

| | RabbitMQ | Azure Service Bus | Kafka / Event Hubs |
|---|---|---|---|
| Model | Queue/Exchange | Queue/Topic | Log (append-only) |
| Mesaj ömrü | Tüketilince silinir | Tüketilince silinir | Retention süresince kalır |
| Replay | ❌ | ❌ | ✅ |
| Sıralama | Kuyruk bazında | Session bazında | Partition bazında |
| Kullanım | İş mesajları, komutlar | Kurumsal, Azure | Yüksek hacim, stream, analitik |

## .NET Kod Örneği (MassTransit)

```csharp
// Contracts (paylaşılan NuGet veya kopyalanmış sözleşme)
public record OrderPlaced(Guid OrderId, Guid CustomerId, decimal Total, DateTime OccurredAt);

// Producer — Ordering
public class PlaceOrderHandler(OrderingDbContext db, IPublishEndpoint bus)
{
    public async Task Handle(PlaceOrder cmd, CancellationToken ct)
    {
        var order = Order.Place(cmd.CustomerId, cmd.Items);
        db.Orders.Add(order);
        await bus.Publish(new OrderPlaced(order.Id, order.CustomerId, order.Total, DateTime.UtcNow), ct);
        await db.SaveChangesAsync(ct);   // Outbox açıksa publish + save atomik
    }
}

// Consumer — Notification
public class SendOrderConfirmation(IEmailSender email) : IConsumer<OrderPlaced>
{
    public async Task Consume(ConsumeContext<OrderPlaced> ctx)
        => await email.SendAsync(ctx.Message.CustomerId, $"Siparişiniz alındı: {ctx.Message.OrderId}");
}

// Program.cs
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SendOrderConfirmation>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
        cfg.ConfigureEndpoints(ctx);
    });
});
```

## Dağıtık Mesajlaşmanın Gerçekleri

1. **At-least-once delivery**: Mesaj **birden fazla** gelebilir → tüketiciler **idempotent** olmalı (bkz. [Inbox](../03-dagitik-desenler/02-outbox-inbox.md)).
2. **Sıralama garantisi yok** (genelde) → `OrderShipped`, `OrderPlaced`'ten önce gelebilir. Versiyon numarası / durum kontrolü kullan.
3. **Poison message**: Sürekli hata veren mesaj → **Dead Letter Queue (DLQ)**.
4. **Atomiklik**: DB'ye yaz + mesaj yayınla atomik değil → **Outbox**.
5. **Şema evrimi**: Alan eklemek güvenli, silmek/yeniden adlandırmak kırıcı. Versiyonlama (`OrderPlacedV2`) veya schema registry.
6. **Gözlemlenebilirlik**: Correlation ID ve OpenTelemetry trace context mesaj header'ında taşınmalı.

## Artıları

- ✅ Maksimum gevşek bağlılık; yeni tüketici eklemek üreticiyi etkilemez.
- ✅ Temporal decoupling: tüketici çökse bile mesajlar kuyrukta bekler.
- ✅ Yük dengeleme (load leveling), ani yükleri emme.
- ✅ Doğal ölçeklenebilirlik (competing consumers).
- ✅ Audit, analitik, gerçek zamanlı akışlar.

## Eksileri

- ❌ Akışı takip etmek zor ("bu olaydan sonra ne oluyor?").
- ❌ Eventual consistency.
- ❌ Hata ayıklama ve test zorluğu.
- ❌ Broker operasyonu, DLQ yönetimi.
- ❌ Olay döngüleri ve "event spagettisi" riski.

## Ne Zaman Kullanılır?

- Mikroservisler ve modüler monolitler arası iletişim
- Bir olaya birden fazla bağımsız tepki gerekiyorsa
- Yüksek ölçek, ani yük, IoT, gerçek zamanlı sistemler
- Uzun süren iş akışları
