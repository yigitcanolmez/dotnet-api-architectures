# 12 — Dayanıklılık (Resilience) Desenleri

İlgili doküman: [Dayanıklılık Desenleri](../../03-dagitik-desenler/05-dayaniklilik-desenleri.md)

Kasıtlı olarak arızalı bir fiyat servisi (`UnstableService.Api`) ve bu servisi **sekiz farklı politikayla**
çağıran bir istemci (`ResilienceClient.Api`). Arızayı çalışma zamanında `POST /chaos` ile değiştirip her
politikanın davranışını ölçüyorsunuz. Paket: `Microsoft.Extensions.Http.Resilience 10.11.0` (Polly v8 / `Polly.Core` üzerine).

## Bu örnekte ne görüyorsunuz

- **Retry** (exponential backoff + jitter), **Timeout**, **Circuit Breaker**, **Bulkhead**, **Fallback**, **Hedging** — her biri ayrı bir named `HttpClient`'ta, izole.
- `AddStandardResilienceHandler()` ve `AddStandardHedgingHandler()` hazır pipeline'larının ayarlarının **açıkça override** edilmesi.
- HTTP dışı, genel amaçlı bir `ResiliencePipeline<T>` (`AddResiliencePipeline` + `ResiliencePipelineProvider`) ile fallback.
- Her yanıtta `upstreamAttempts` ve `elapsedMs`: politikanın **bedelini** (ek istek, süre) görünür kılar; `GET /stats` downstream tarafından sayımı gösterir.
- Gelen istekler için ASP.NET Core **rate limiting** (token bucket) → 429.
- `chaos-demo.sh`: tüm senaryoları otomatik koşturup özet basar.

## Klasör ağacı

```
12-Resilience/
├── Resilience.slnx
├── chaos-demo.sh                          # senaryoları otomatik koşturur (servisleri gerekirse kendisi başlatır)
└── src/
    ├── UnstableService.Api/               # :5301 — güvenilmez bağımlılık
    │   ├── Program.cs                     # GET /prices/{id}, GET|POST /chaos, GET /stats, POST /stats/reset
    │   ├── ChaosState.cs                  # Mode (healthy|flaky|down|slow), FailureRate, LatencyMs, LatencyRate + sayaçlar
    │   └── appsettings.json               # başlangıç chaos ayarları ("Chaos" bölümü)
    └── ResilienceClient.Api/              # :5300 — çağıran servis
        ├── Program.cs                     # DI + token bucket rate limiter + /rate-limited
        ├── Infrastructure/
        │   ├── ResiliencePolicies.cs      # ★ 8 named client + fallback pipeline'ı: TÜM politikalar burada
        │   ├── AttemptCounter.cs          # resilience handler'ın İÇİNE eklenen sayaç (deneme sayısı)
        │   ├── LastKnownPriceCache.cs     # fallback için son bilinen fiyat
        │   └── PriceDto.cs
        └── Endpoints/PriceEndpoints.cs    # /naive /retry /timeout /circuit-breaker /standard /fallback /bulkhead /hedging
```

## Referans grafiği ve handler zinciri

```
ResilienceClient.Api ──HTTP──► UnstableService.Api       (ProjectReference YOK; yalnızca HTTP)

Bir named HttpClient'ın içi (dıştan içe):
  Endpoint ─► [Resilience handler: retry / timeout / CB / ...] ─► [AttemptCountingHandler] ─► ağ
                       ▲ bir mantıksal istek                         ▲ her DENEME buradan geçer
```

Neden bu yönde? Politika handler'ı **dışta** olmalı ki her yeniden denemede içteki handler'lar tekrar çalışsın.
`AttemptCountingHandler` politikadan *sonra* eklendiği için içte kalır ve her retry/hedge denemesini sayar.

## Bir isteğin yolculuğu: `GET /retry/{id}` (chaos: flaky %50)

> Bu örnekte sipariş yok; şartnamedeki `POST /orders/{id}/cancel` yerine örneğin merkezindeki çağrıyı izliyoruz.

1. `Endpoints/PriceEndpoints.cs → Call("retry", ...)`: `AttemptCounter.Begin()`, `IHttpClientFactory.CreateClient("retry")`.
2. `Infrastructure/ResiliencePolicies.cs`: "retry" client'ının `retry-pipeline` handler'ı isteği alır.
3. `AttemptCountingHandler` sayacı artırır → `UnstableService.Api/Program.cs` `/prices/{id}`: `ChaosState` %50 olasılıkla **500** döner.
4. Retry stratejisi 500'ü geçici hata sayar (`HttpRetryStrategyOptions` varsayılan `ShouldHandle`), `OnRetry` log'u yazılır, ~200 ms (+jitter) bekler, 3. adımı tekrarlar (en çok 3 tekrar: ~200 → ~400 → ~800 ms).
5. Başarılı yanıt (veya son başarısız yanıt) endpoint'e döner → `200 {source:"live", upstreamAttempts:N}` ya da `502 ProblemDetails`.

## Çalıştırma

```bash
cd samples/12-Resilience
dotnet build
dotnet run --project src/UnstableService.Api      # terminal 1
dotnet run --project src/ResilienceClient.Api     # terminal 2
./chaos-demo.sh                                   # veya: servisler kapalıyken bile çalışır, kendisi başlatıp durdurur
```

Test projesi yok: davranış olasılıksal ve zamanlamaya bağlı; doğrulama `chaos-demo.sh` ile yapılır.

```bash
P=11111111-1111-1111-1111-111111111111
curl -s -X POST localhost:5301/chaos -H 'Content-Type: application/json' -d '{"mode":"flaky","failureRate":0.5}'
curl -s localhost:5300/retry/$P
curl -s localhost:5301/stats
```

## Deney tablosu

| Endpoint | Chaos ayarı | Ne gözlemlenir | Örnek sonuç (`chaos-demo.sh`) |
|---|---|---|---|
| `/naive` | `flaky 0.5` | Hataların yarısı doğrudan istemciye | ~10/20 başarı, 20 downstream istek |
| `/retry` | `flaky 0.5` | Başarı ≈ 1−0.5⁴ ≈ %94; **downstream yükü artar** | ~19/20 başarı, ~32 downstream istek |
| `/standard` | `flaky 0.2` | Retry + CB + iki katmanlı timeout birlikte | ~20/20 başarı, ~25 downstream istek |
| `/timeout` | `slow 3000` | 1 sn'de vazgeçer → 504 | naive 3.0 sn/200, timeout 1.0 sn/504 |
| `/circuit-breaker` | `down` | 5 hatadan sonra devre **Open**; sonrakiler ağa çıkmadan 503 | `502×5, 503×7`, 5/12 downstream istek |
| `/fallback` | önce `healthy`, sonra `down` | `source: live` → `source: fallback` (`last-known-price` / `default-price`) | her zaman 200 |
| `/bulkhead` | `slow 2000`, 8 paralel | 2 eşzamanlı + 1 kuyruk geçer, kalanlar anında 503 | 3×200, 5×503, maxConcurrent=2 |
| `/hedging` | `slow 2000`, `latencyRate 0.5` | 300 ms sonra paralel deneme, ilk gelen kazanır | ort. 1.2 sn → 0.25 sn, 10 → ~18 istek |
| `/rate-limited` | — | Token bucket 5 jeton → 6. istekte 429 + `Retry-After` | `200×5, 429×3` |

### naive vs retry başarı oranını kendiniz ölçün

```bash
P=11111111-1111-1111-1111-111111111111
curl -s -X POST localhost:5301/chaos -H 'Content-Type: application/json' -d '{"mode":"flaky","failureRate":0.5}'
for ep in naive retry; do
  curl -s -X POST localhost:5301/stats/reset
  ok=0; for i in $(seq 1 50); do
    [ "$(curl -s -o /dev/null -w '%{http_code}' localhost:5300/$ep/$P)" = 200 ] && ok=$((ok+1))
  done
  echo "$ep: $ok/50 başarılı, downstream: $(curl -s localhost:5301/stats | grep -o '"totalRequests":[0-9]*')"
done
```

### Circuit breaker'ın açıldığını gözlemleme

```bash
curl -s -X POST localhost:5301/chaos -H 'Content-Type: application/json' -d '{"mode":"down"}'
for i in $(seq 1 8); do curl -s localhost:5300/circuit-breaker/$P | grep -o '"status":[0-9]*,\|"circuitState":"[A-Za-z]*"' | tr '\n' ' '; echo; done
curl -s localhost:5300/circuit-breaker/state           # {"circuitState":"Open"}
curl -s -X POST localhost:5301/chaos -H 'Content-Type: application/json' -d '{"mode":"healthy"}'
sleep 15; curl -s localhost:5300/circuit-breaker/$P    # Half-Open deneme başarılı → Closed
```

İstemci log'unda `[circuit-breaker] OPEN ⛔`, `HALF-OPEN 🟡`, `CLOSED ✅` satırlarını görürsünüz
(`OnOpened/OnHalfOpened/OnClosed` + `CircuitBreakerStateProvider`).

## ⚠️ Retry storm uyarısı

Retry "bedava" değildir: yukarıdaki tabloda `/retry` aynı 20 çağrı için downstream'e ~%60 **daha fazla** istek gönderdi.
Zincirde her katman 3 tekrar yaparsa (gateway → servis A → servis B) en alttaki servis tek bir kullanıcı isteği için
`4 × 4 × 4 = 64` istek alabilir — zaten zorlanan servisi tamamen çökertir. Kurallar:
- Retry'ı **tek katmanda** yapın (genelde en dıştaki çağıran istemcide), diğer katmanlarda kapatın.
- **Jitter** kullanın (eş zamanlı tekrarlar senkronize dalgalar oluşturmasın).
- Retry'ı **circuit breaker** ile birlikte kullanın (`/standard` gibi): servis tamamen çöktüyse tekrar etmek yerine hızlı başarısız ol.
- Sadece **idempotent** işlemleri tekrar edin (GET/PUT/DELETE veya idempotency key'li POST).

## Timeout hiyerarşisi

Timeout'lar dıştan içe **azalmalı**; içteki süre dıştakinden uzunsa dıştaki her zaman önce tetiklenir ve içteki anlamsızlaşır.

```
İstemci / Gateway (ör. 10 sn)
  └─ ResilienceClient: HttpClient.Timeout (100 sn varsayılan — standard handler kullanırken dokunulmaz)
       └─ TotalRequestTimeout  5 sn   ← tüm denemeler + beklemeler dahil (/standard)
            └─ Retry (3 tekrar, 200 ms → exponential + jitter)
                 └─ AttemptTimeout 1 sn   ← tek bir deneme
                      └─ UnstableService (DB çağrısı vb. < 1 sn olmalı)
```

`/standard`'da `CircuitBreaker.SamplingDuration` (10 sn) en az `2 × AttemptTimeout` olmalıdır; aksi halde
`Microsoft.Extensions.Http.Resilience` uygulama açılışında seçenek doğrulama hatası verir.

## Deneyin

1. `/retry`'ın `MaxRetryAttempts` değerini 10 yapın, `flaky 0.9` ile `GET /stats` üzerinden downstream yükünü ölçün — retry storm'u kendi gözünüzle görün.
2. Fallback pipeline'ına (`ResiliencePolicies.cs`) bir `AddCircuitBreaker` ekleyin: servis `down` iken fallback yanıtlarının `elapsedMs` değeri ve `upstreamAttempts` nasıl değişiyor?
3. `/standard` için `AttemptTimeout`'u 3 sn, `TotalRequestTimeout`'u 2 sn yapın ve `slow 2500` ile deneyin: hangi timeout tetikleniyor, neden hiyerarşi bozuldu?

## Artı / eksi gözlemleri

- ✅ Politikalar tamamen **altyapı**: endpoint kodu tüm client'lar için aynı; politika sadece DI kaydıyla değişiyor.
- ✅ `AddStandardResilienceHandler` mantıklı bir sırayla (rate limiter → total timeout → retry → CB → attempt timeout) hazır gelir.
- ✅ Fallback + bulkhead + CB gibi desenler kullanıcıya hatayı yansıtmadan "degrade" çalışmayı mümkün kılar.
- ❌ Her politika ek yük ya da gecikme getirir (retry/hedging → daha çok downstream isteği); yanlış ayar sorunu büyütür.
- ❌ Olasılıksal davranış test etmesi zor; değerlerin (eşik, süre) üretim metrikleriyle ayarlanması gerekir.
- ❌ En iyi dayanıklılık çoğu zaman senkron çağrıyı hiç yapmamaktır (asenkron mesajlaşma) — bu örnek bunu çözmez.
