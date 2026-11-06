using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Retry;

namespace ResilienceClient.Api.Infrastructure;

// [Dayanıklılık politikaları] Her politika AYRI bir named HttpClient'a bağlanır ki etkileri izole gözlemlensin.
// Handler sırası: [resilience handler] → [AttemptCountingHandler] → ağ. (Sonra eklenen handler içte kalır.)
public static class ResiliencePolicies
{
    public const string Naive = "naive";
    public const string Retry = "retry";
    public const string Timeout = "timeout";
    public const string CircuitBreaker = "circuit-breaker";
    public const string Standard = "standard";
    public const string FallbackUpstream = "fallback-upstream";
    public const string Bulkhead = "bulkhead";
    public const string Hedging = "hedging";

    public const string FallbackPipeline = "pricing-fallback";
    public static readonly ResiliencePropertyKey<Guid> ProductIdKey = new("productId");

    public static IServiceCollection AddPriceClients(this IServiceCollection services, IConfiguration config)
    {
        var baseUrl = new Uri(config["Unstable:BaseUrl"]!);
        services.AddTransient<AttemptCountingHandler>();
        services.AddSingleton<LastKnownPriceCache>();

        // Her client: [politika (varsa)] → [AttemptCountingHandler] → ağ.
        void Client(string name, Action<IHttpClientBuilder>? policy = null)
        {
            var builder = services.AddHttpClient(name, c => c.BaseAddress = baseUrl);
            policy?.Invoke(builder);
            builder.AddHttpMessageHandler<AttemptCountingHandler>(); // politikadan SONRA eklendiği için içte kalır
        }

        // 1) NAIVE: hiçbir politika yok. Tek koruma HttpClient.Timeout (varsayılan 100 sn!).
        Client(Naive);

        // 2) RETRY: exponential backoff + jitter. 200ms, ~400ms, ~800ms (+rastgele sapma).
        //    Varsayılan ShouldHandle: 5xx, 408, 429, HttpRequestException, TimeoutRejectedException.
        Client(Retry, c => c
            .AddResilienceHandler("retry-pipeline", (b, ctx) => b.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200),
                OnRetry = args =>
                {
                    Log(ctx.ServiceProvider).LogInformation("[retry] deneme #{Attempt} başarısız ({Outcome}); {Delay} ms sonra tekrar",
                        args.AttemptNumber + 1, Describe(args.Outcome), (int)args.RetryDelay.TotalMilliseconds);
                    return default;
                }
            })));

        // 3) TIMEOUT: her denemeye 1 sn üst sınır. Aşılırsa TimeoutRejectedException → 504.
        Client(Timeout, c => c.AddResilienceHandler("timeout-pipeline", b => b.AddTimeout(TimeSpan.FromSeconds(1))));

        // 4) CIRCUIT BREAKER: 10 sn'lik pencerede en az 5 istek ve hata oranı ≥ %50 → 15 sn OPEN.
        //    OPEN iken istek ağa HİÇ çıkmaz (BrokenCircuitException) → downstream'i nefes aldırır.
        services.AddSingleton<CircuitBreakerStateProvider>();
        Client(CircuitBreaker, c => c
            .AddResilienceHandler("cb-pipeline", (b, ctx) =>
            {
                var log = Log(ctx.ServiceProvider);
                b.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 5,
                    SamplingDuration = TimeSpan.FromSeconds(10),
                    BreakDuration = TimeSpan.FromSeconds(15),
                    StateProvider = ctx.ServiceProvider.GetRequiredService<CircuitBreakerStateProvider>(),
                    OnOpened = a => { log.LogWarning("[circuit-breaker] OPEN ⛔ ({Duration} sn)", a.BreakDuration.TotalSeconds); return default; },
                    OnHalfOpened = _ => { log.LogWarning("[circuit-breaker] HALF-OPEN 🟡 deneme isteğine izin veriliyor"); return default; },
                    OnClosed = _ => { log.LogWarning("[circuit-breaker] CLOSED ✅"); return default; }
                });
            }));

        // 5) STANDARD: rate limiter → total timeout → retry → circuit breaker → attempt timeout.
        //    Varsayılanlar açıkça override ediliyor (değerleri görmek öğretici).
        Client(Standard, c => c
            .AddStandardResilienceHandler(o =>
            {
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);     // dış sınır: tüm denemeler + beklemeler
                o.Retry.MaxRetryAttempts = 3;
                o.Retry.BackoffType = DelayBackoffType.Exponential;
                o.Retry.UseJitter = true;
                o.Retry.Delay = TimeSpan.FromMilliseconds(200);
                o.CircuitBreaker.FailureRatio = 0.5;
                o.CircuitBreaker.MinimumThroughput = 20;
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10); // ≥ 2 × AttemptTimeout olmalı (doğrulanır)
                o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(1);           // iç sınır: tek deneme
            }));

        // 6) FALLBACK: HTTP handler değil, genel amaçlı bir ResiliencePipeline<PriceDto> (Polly.Core).
        //    Fallback (en dış) → Timeout(1 sn). Hata/timeout → son bilinen fiyat ya da varsayılan.
        Client(FallbackUpstream);
        services.AddResiliencePipeline<string, PriceResult>(FallbackPipeline, (b, ctx) => b
            .AddFallback(new FallbackStrategyOptions<PriceResult>
            {
                ShouldHandle = new PredicateBuilder<PriceResult>().Handle<Exception>(),
                FallbackAction = args =>
                {
                    var cache = ctx.ServiceProvider.GetRequiredService<LastKnownPriceCache>();
                    var id = args.Context.Properties.GetValue(ProductIdKey, Guid.Empty);
                    var reason = Describe(args.Outcome);
                    var result = cache.Get(id) is { } cached
                        ? new PriceResult(cached, "fallback", "last-known-price", reason)
                        : new PriceResult(new PriceDto(id, "Bilinmiyor", 0m, "TRY"), "fallback", "default-price", reason);
                    return Outcome.FromResultAsValueTask(result);
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(1)));

        // 7) BULKHEAD: aynı anda en fazla 2 istek + 1 kuyrukta. Fazlası anında reddedilir
        //    (RateLimiterRejectedException) → yavaş bağımlılık TÜM thread/bağlantıları tüketemez.
        Client(Bulkhead, c => c.AddResilienceHandler("bulkhead-pipeline", b => b.AddConcurrencyLimiter(permitLimit: 2, queueLimit: 1)));

        // 8) HEDGING: ilk deneme 300 ms içinde dönmezse paralel ikinci (ve üçüncü) deneme; ilk gelen kazanır.
        Client(Hedging, c => c
            .AddStandardHedgingHandler()
            .Configure(o =>
            {
                o.Hedging.MaxHedgedAttempts = 2;
                o.Hedging.Delay = TimeSpan.FromMilliseconds(300);
                o.Endpoint.Timeout.Timeout = TimeSpan.FromSeconds(5);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
            }));

        return services;
    }

    private static ILogger Log(IServiceProvider sp) => sp.GetRequiredService<ILoggerFactory>().CreateLogger("Resilience");

    private static string Describe<T>(Outcome<T> outcome) => outcome switch
    {
        { Exception: { } ex } => ex.GetType().Name,
        { Result: HttpResponseMessage r } => $"HTTP {(int)r.StatusCode}",
        _ => "?"
    };

    internal static bool IsSuccess(HttpStatusCode code) => (int)code is >= 200 and < 300;
}

public sealed record PriceResult(PriceDto Price, string Source, string? FallbackKind = null, string? FallbackReason = null);
