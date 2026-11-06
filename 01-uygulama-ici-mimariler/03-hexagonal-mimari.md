# 3. Hexagonal Mimari (Ports & Adapters)

> 💻 **Çalışan örnek proje:** [`samples/03-Hexagonal`](../samples/03-Hexagonal/)

> Alistair Cockburn, 2005

## Neden Ortaya Çıktı?

Cockburn şunu gözlemledi: uygulamalar hep "üstte UI, altta DB" diye düşünülüyordu. Ama gerçekte bir uygulamanın **birçok dış etkileşimi** vardır: HTTP istemcisi, CLI, test runner, message queue, cron job, veritabanı, e-posta servisi, 3. parti API...

Sorunlar:
- İş mantığı UI'ya sızıyordu → aynı mantığı CLI'dan ya da kuyruktan tetiklemek imkânsızdı.
- Otomatik test yazmak için UI'yı sürmek gerekiyordu.
- "Üst/alt" kavramı yanıltıcıydı; DB de UI de aslında **dış dünya**dır.

## Çözdüğü Problem

> "Uygulama; kullanıcı, program, otomatik test veya batch script tarafından eşit şekilde sürülebilsin ve çalışma zamanındaki cihaz/veritabanlarından izole şekilde geliştirilip test edilebilsin."

## Yapı

```
          Driving (Primary) side                 Driven (Secondary) side
       (uygulamayı TETİKLEYENLER)              (uygulamanın KULLANDIKLARI)

   [REST Controller]──┐                      ┌──►[EF Core Adapter] ──► SQL
   [gRPC Service]─────┤                      │
   [RabbitMQ Consumer]┼─►(Port: IPlaceOrder) │ (Port: IOrderRepository)
   [xUnit Test]───────┘      │               │
                        ┌────▼───────────────┴──┐
                        │   APPLICATION CORE    │──►(Port: IPaymentGateway)──►[Stripe Adapter]
                        │  (Use cases + Domain) │
                        └───────────────────────┘──►(Port: INotifier)──►[SMTP Adapter]
```

- **Port**: Çekirdeğin tanımladığı arayüz (sözleşme).
  - **Driving/Inbound port**: Çekirdeğin dışarıya sunduğu use case arayüzü (`IPlaceOrderUseCase`).
  - **Driven/Outbound port**: Çekirdeğin ihtiyaç duyduğu hizmet arayüzü (`IOrderRepository`, `IPaymentGateway`).
- **Adapter**: Port'u belirli bir teknolojiye bağlayan implementasyon.
  - **Driving adapter**: Controller, consumer, CLI komutu, test.
  - **Driven adapter**: EF repository, HTTP client, SMTP sender, in-memory fake.

Altıgen şeklinin hiçbir anlamı yok; sadece "birden fazla kenar/port olabilir" demek için seçilmiş.

## .NET Klasör Yapısı

```
src/
├── Ordering.Core/
│   ├── Domain/Order.cs
│   ├── Ports/
│   │   ├── In/IPlaceOrderUseCase.cs
│   │   └── Out/IOrderRepository.cs, IPaymentGateway.cs
│   └── UseCases/PlaceOrderService.cs
├── Ordering.Adapters.Web/          (driving)  OrdersEndpoints.cs
├── Ordering.Adapters.Messaging/    (driving)  PlaceOrderConsumer.cs
├── Ordering.Adapters.Persistence/  (driven)   EfOrderRepository.cs
├── Ordering.Adapters.Payments/     (driven)   StripePaymentGateway.cs
└── Ordering.Host/                  Program.cs (wiring)
```

## Kod Örneği

```csharp
// ---------- Core: Ports ----------
public interface IPlaceOrderUseCase                // inbound port
{
    Task<Guid> ExecuteAsync(PlaceOrderCommand cmd, CancellationToken ct);
}
public interface IPaymentGateway                    // outbound port
{
    Task<PaymentResult> ChargeAsync(Guid customerId, decimal amount, CancellationToken ct);
}

// ---------- Core: Use case ----------
public class PlaceOrderService(IOrderRepository orders, IPaymentGateway payments) : IPlaceOrderUseCase
{
    public async Task<Guid> ExecuteAsync(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var order = Order.Create(cmd.CustomerId, cmd.Lines);
        var result = await payments.ChargeAsync(cmd.CustomerId, order.Total, ct);
        if (!result.Success) throw new PaymentFailedException(result.Reason);
        order.MarkPaid(result.TransactionId);
        await orders.AddAsync(order, ct);
        return order.Id;
    }
}

// ---------- Driving adapter #1: HTTP ----------
app.MapPost("/orders", async (PlaceOrderCommand cmd, IPlaceOrderUseCase uc, CancellationToken ct)
    => Results.Created($"/orders/{await uc.ExecuteAsync(cmd, ct)}", null));

// ---------- Driving adapter #2: Message queue (aynı use case!) ----------
public class PlaceOrderConsumer(IPlaceOrderUseCase uc) : IConsumer<PlaceOrderCommand>
{
    public Task Consume(ConsumeContext<PlaceOrderCommand> ctx) => uc.ExecuteAsync(ctx.Message, ctx.CancellationToken);
}

// ---------- Driven adapter: Stripe ----------
public class StripePaymentGateway(HttpClient http) : IPaymentGateway { /* ... */ }

// ---------- Test adapter (driven) ----------
public class FakePaymentGateway : IPaymentGateway
{
    public Task<PaymentResult> ChargeAsync(Guid c, decimal a, CancellationToken ct) => Task.FromResult(PaymentResult.Ok("tx-1"));
}
```

## Artıları

- ✅ Aynı iş mantığı HTTP, gRPC, kuyruk, CLI'dan tetiklenebilir.
- ✅ Dış sistemler kolayca fake'lenir → hızlı, deterministik testler.
- ✅ Teknoloji değişimi sadece adapter değişimidir.
- ✅ "Dış dünya" kavramını simetrik düşünmeyi öğretir.

## Eksileri

- ❌ Port/adapter sayısı hızla artar.
- ❌ İç yapının (domain, use case) nasıl organize edileceğini söylemez.
- ❌ Basit uygulamalarda aşırı soyutlama.

## Onion / Hexagonal / Clean İlişkisi

Üçü de **aynı fikrin** farklı anlatımlarıdır: *Dependency Inversion ile çekirdeği altyapıdan izole et.*

- **Hexagonal**: İçeriyi/dışarıyı ve port/adapter kavramını vurgular. İç yapı hakkında konuşmaz.
- **Onion**: İç yapıyı (domain model, domain services, app services) katmanlara böler.
- **Clean**: İkisini birleştirip Use Case (Interactor) kavramını merkeze koyar.
