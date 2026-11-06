# 19. Dayanıklılık (Resilience) Desenleri

> 💻 **Çalışan örnek proje:** [`samples/12-Resilience`](../samples/12-Resilience/)

## Neden Ortaya Çıktı? — Dağıtık Hesaplamanın 8 Yanılgısı (Peter Deutsch)

1. Ağ güvenilirdir. 2. Gecikme sıfırdır. 3. Bant genişliği sonsuzdur. 4. Ağ güvenlidir.
5. Topoloji değişmez. 6. Tek bir yönetici vardır. 7. Taşıma maliyeti sıfırdır. 8. Ağ homojendir.

Bunların **hepsi yanlış**. Servisler arası her çağrı başarısız olabilir, yavaşlayabilir, zaman aşımına uğrayabilir. Tek bir yavaş bağımlılık:
- Thread/bağlantı havuzlarını tüketir,
- Çağıran servisi de yavaşlatır,
- **Kademeli çöküşe (cascading failure)** yol açar.

## Desenler ve Çözdükleri Problemler

| Desen | Problem | Çözüm |
|-------|---------|-------|
| **Timeout** | Yanıt vermeyen servis kaynakları sonsuza kadar tutar | Her çağrıya üst süre sınırı |
| **Retry** | Geçici (transient) hatalar: ağ kesintisi, 503, deadlock | Exponential backoff + jitter ile tekrar dene |
| **Circuit Breaker** | Çökmüş servise ısrarla istek atmak hem onu hem seni öldürür | Hata oranı eşiği aşılınca devreyi aç, hızlı başarısız ol, bir süre sonra yarı-açık test et |
| **Bulkhead** | Bir bağımlılık tüm kaynakları tüketir | Kaynakları (thread, bağlantı) bağımlılık başına izole et |
| **Fallback** | Bağımlılık yoksa kullanıcıya hata döner | Varsayılan değer, cache, degrade mod |
| **Hedging** | Kuyruk gecikmesi (p99) yüksek | Yavaş istekte paralel ikinci istek gönder, ilk geleni al |
| **Rate Limiting** | Aşırı yük, kötüye kullanım | İstek sayısını sınırla |
| **Load Shedding** | Sistem kapasitesi aşıldı | Düşük öncelikli istekleri reddet |

### Circuit Breaker Durumları
```
        hata oranı > eşik
 CLOSED ─────────────────► OPEN ──(break süresi doldu)──► HALF-OPEN
   ▲                        ▲                                │
   │      deneme başarılı   │       deneme başarısız         │
   └────────────────────────┼────────────────────────────────┘
                            └────────────────────────────────┘
```

## .NET — Microsoft.Extensions.Http.Resilience (Polly v8 üzerine)

```csharp
// Hazır standart pipeline: rate limiter → total timeout → retry → circuit breaker → attempt timeout
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("http://catalog"))
    .AddStandardResilienceHandler(o =>
    {
        o.Retry.MaxRetryAttempts = 3;
        o.Retry.BackoffType = DelayBackoffType.Exponential;
        o.Retry.UseJitter = true;
        o.CircuitBreaker.FailureRatio = 0.5;
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
    });

// Hedging
builder.Services.AddHttpClient<SearchClient>().AddStandardHedgingHandler();
```

### Özel pipeline (HTTP dışı)

```csharp
builder.Services.AddResiliencePipeline<string, decimal>("pricing", b => b
    .AddFallback(new() { FallbackAction = _ => Outcome.FromResultAsValueTask(0m) })
    .AddCircuitBreaker(new() { FailureRatio = 0.5, MinimumThroughput = 10 })
    .AddConcurrencyLimiter(20)            // bulkhead
    .AddTimeout(TimeSpan.FromSeconds(1)));

public class PriceService(ResiliencePipelineProvider<string> p)
{
    public ValueTask<decimal> GetAsync(Guid id) =>
        p.GetPipeline<decimal>("pricing").ExecuteAsync(async ct => await FetchPrice(id, ct));
}
```

### ASP.NET Core rate limiting (gelen istekler)
```csharp
builder.Services.AddRateLimiter(o => o.AddTokenBucketLimiter("api", x =>
    { x.TokenLimit = 100; x.TokensPerPeriod = 50; x.ReplenishmentPeriod = TimeSpan.FromSeconds(10); }));
app.MapGroup("/api").RequireRateLimiting("api");
```

## Dikkat Edilecekler
- ⚠️ **Retry sadece idempotent işlemlerde** (GET, PUT, DELETE veya idempotency key'li POST).
- ⚠️ **Retry storm**: Her katman 3 kez retry ederse 3 katmanda 27 istek → retry'ı tek katmanda yapın, jitter kullanın.
- ⚠️ Timeout'lar dıştan içe **azalan** olmalı (gateway 10s > servis 5s > DB 2s).
- ⚠️ Circuit breaker durumunu metrik olarak izleyin.
- ⚠️ En iyi dayanıklılık: senkron bağımlılığı **asenkron mesajlaşmaya** çevirmek.
