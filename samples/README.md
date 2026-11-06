# Örnek Projeler

Her mimari için **derlenen, test edilen ve çalışan** bir .NET 10 örneği. Karşılaştırmayı kolaylaştırmak için örneklerin çoğu **aynı domain'i** kullanır:

> **Sipariş sistemi**: Ürünler (Klavye, Mouse, Monitör) · Sipariş oluştur / getir / kargola / iptal et
> Kurallar: 1–10 kalem, adet > 0, sadece `Placed` sipariş kargolanır, `Shipped` sipariş iptal edilemez.

Aynı kuralın her mimaride **hangi dosyada** yaşadığını ve bir isteğin **hangi katmanlardan** geçtiğini karşılaştırın. Her örneğin README'sinde "bir isteğin yolculuğu" bölümü var.

| # | Örnek | Mimari / Desen | Port | Doküman |
|---|-------|----------------|------|---------|
| 01 | [01-Layered](01-Layered/) | Katmanlı (N-Tier), Controller'lar | 5001 | [doc](../01-uygulama-ici-mimariler/01-katmanli-mimari.md) |
| 02 | [02-Onion](02-Onion/) | Onion + mimari testleri | 5002 | [doc](../01-uygulama-ici-mimariler/02-onion-mimarisi.md) |
| 03 | [03-Hexagonal](03-Hexagonal/) | Ports & Adapters (HTTP + kuyruk driving adapter) | 5003 | [doc](../01-uygulama-ici-mimariler/03-hexagonal-mimari.md) |
| 04 | [04-CleanArchitecture](04-CleanArchitecture/) | Clean + DDD taktik + MediatR + FluentValidation | 5004 | [doc](../01-uygulama-ici-mimariler/04-clean-architecture.md), [DDD](../01-uygulama-ici-mimariler/06-domain-driven-design.md) |
| 05 | [05-VerticalSlice](05-VerticalSlice/) | VSA + REPR (MediatR'sız) | 5005 | [doc](../01-uygulama-ici-mimariler/05-vertical-slice-architecture.md) |
| 06 | [06-CQRS](06-CQRS/) | CQRS (ayrı read model, eventual consistency) | 5006 | [doc](../01-uygulama-ici-mimariler/07-cqrs.md) |
| 07 | [07-EventSourcing](07-EventSourcing/) | Event Sourcing (BankAccount), projeksiyon, temporal query | 5007 | [doc](../01-uygulama-ici-mimariler/08-event-sourcing.md) |
| 08 | [08-ModularMonolith](08-ModularMonolith/) | Modüler Monolit + Outbox/Inbox + integration events | 5008 | [doc](../01-uygulama-ici-mimariler/09-moduler-monolit.md) |
| 09 | [09-SagaChoreography](09-SagaChoreography/) | Choreography Saga (tek process, MassTransit in-memory) | 5009 | [doc](../03-dagitik-desenler/01-saga.md) |
| 10 | [10-Microservices](10-Microservices/) | Mikroservis + EDA + Orchestration Saga + Outbox + YARP Gateway + Resilience (RabbitMQ) | 5100–5103 | [doc](../02-dagitik-mimariler/02-mikroservisler.md), [Saga](../03-dagitik-desenler/01-saga.md) |
| 11 | [11-StranglerFig](11-StranglerFig/) | Strangler Fig + Anti-Corruption Layer | 5200–5202 | [doc](../03-dagitik-desenler/04-strangler-fig.md) |
| 12 | [12-Resilience](12-Resilience/) | Retry, Timeout, Circuit Breaker, Fallback, Bulkhead, Hedging | 5300–5301 | [doc](../03-dagitik-desenler/05-dayaniklilik-desenleri.md) |
| 13 | [13-SOA](13-SOA/) | SOA: CoreWCF SOAP servisi + orkestrasyon servisi | 5400–5401 | [doc](../02-dagitik-mimariler/01-soa.md) |

> Serverless (Azure Functions) için örnek eklenmedi; çalıştırmak Azure Functions Core Tools gerektirir. Kod örneği [dokümanda](../02-dagitik-mimariler/04-serverless.md) mevcut.

## Gereksinimler

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Sadece `10-Microservices` için: Docker (RabbitMQ)

## Hızlı Başlangıç

```bash
cd samples/02-Onion
dotnet build
dotnet test
dotnet run --project src/Onion.Api      # klasör yapısı örneğe göre değişir, örnek README'sine bakın
```

Her API projesinde bir `.http` dosyası vardır (VS / VS Code REST Client / Rider ile doğrudan istek atabilirsiniz).

## Önerilen Çalışma Sırası

1. **01 → 02 → 03 → 04**: Bağımlılık yönünün nasıl tersine döndüğünü izleyin (Katmanlı → Onion → Hexagonal → Clean).
   - `Cancel` kuralı 01'de `OrderService` içinde, 02-04'te `Order` entity'si içinde.
2. **04 vs 05**: Aynı özelliğin yatay (Clean) ve dikey (VSA) organizasyonunu karşılaştırın.
3. **06 → 07**: Okuma/yazma ayrımından olay kaynaklı duruma.
4. **08**: Modüller arası senkron/asenkron iletişim, outbox.
5. **09 → 10**: Choreography'den orchestration'a; tek process'ten gerçek dağıtık sisteme.
6. **11, 12, 13**: Geçiş, dayanıklılık ve tarihsel SOA.

## Notlar

- Veritabanı olarak SQLite kullanılır ve başlangıçta `EnsureCreated()` ile oluşturulur. **Migration kullanılmamıştır** (öğretim sadeliği); gerçek projede `dotnet ef migrations` kullanın.
- MediatR 12.5.0 (son Apache lisanslı sürüm) ve MassTransit 8.x (Apache lisanslı) kullanılmıştır. Yeni sürümleri ticari lisanslıdır.
