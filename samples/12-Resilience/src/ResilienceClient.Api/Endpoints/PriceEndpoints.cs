using System.Diagnostics;
using System.Net.Http.Json;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;
using ResilienceClient.Api.Infrastructure;
using static ResilienceClient.Api.Infrastructure.ResiliencePolicies;

namespace ResilienceClient.Api.Endpoints;

// Aynı downstream çağrısı, farklı politikalı client'larla. Yanıttaki "upstreamAttempts" ve "elapsedMs"
// alanları politikanın etkisini görünür kılar.
public static class PriceEndpoints
{
    public static void MapPriceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/naive/{id:guid}", (Guid id, IHttpClientFactory f, CancellationToken ct) => Call(Naive, f, id, ct));
        app.MapGet("/retry/{id:guid}", (Guid id, IHttpClientFactory f, CancellationToken ct) => Call(Retry, f, id, ct));
        app.MapGet("/timeout/{id:guid}", (Guid id, IHttpClientFactory f, CancellationToken ct) => Call(ResiliencePolicies.Timeout, f, id, ct));
        app.MapGet("/standard/{id:guid}", (Guid id, IHttpClientFactory f, CancellationToken ct) => Call(Standard, f, id, ct));
        app.MapGet("/bulkhead/{id:guid}", (Guid id, IHttpClientFactory f, CancellationToken ct) => Call(Bulkhead, f, id, ct));
        app.MapGet("/hedging/{id:guid}", (Guid id, IHttpClientFactory f, CancellationToken ct) => Call(Hedging, f, id, ct));

        app.MapGet("/circuit-breaker/{id:guid}", (Guid id, IHttpClientFactory f, CircuitBreakerStateProvider cb, CancellationToken ct) =>
            Call(CircuitBreaker, f, id, ct, extra: () => new() { ["circuitState"] = cb.CircuitState.ToString() }));
        app.MapGet("/circuit-breaker/state", (CircuitBreakerStateProvider cb) => new { circuitState = cb.CircuitState.ToString() });

        app.MapGet("/fallback/{id:guid}", Fallback);
    }

    private static async Task<IResult> Call(string policy, IHttpClientFactory factory, Guid id, CancellationToken ct,
        Func<Dictionary<string, object?>>? extra = null)
    {
        var attempts = AttemptCounter.Begin();
        var sw = Stopwatch.StartNew();
        Dictionary<string, object?> Meta() => new(extra?.Invoke() ?? []) { ["policy"] = policy, ["upstreamAttempts"] = attempts.Value, ["elapsedMs"] = sw.ElapsedMilliseconds };

        try
        {
            using var response = await factory.CreateClient(policy).GetAsync($"/prices/{id}", ct);
            if (!response.IsSuccessStatusCode)
                return Results.Problem($"Downstream HTTP {(int)response.StatusCode} döndü.", statusCode: 502, title: "Bad Gateway", extensions: Meta());

            var price = await response.Content.ReadFromJsonAsync<PriceDto>(ct);
            var body = Meta();
            body["source"] = "live";
            body["price"] = price;
            return Results.Ok(body);
        }
        catch (BrokenCircuitException)
        {
            return Results.Problem("Devre AÇIK: istek downstream'e gönderilmeden hızlıca reddedildi.", statusCode: 503, title: "Circuit Open", extensions: Meta());
        }
        catch (TimeoutRejectedException)
        {
            return Results.Problem("Polly timeout: downstream zamanında yanıt vermedi.", statusCode: 504, title: "Gateway Timeout", extensions: Meta());
        }
        catch (RateLimiterRejectedException)
        {
            return Results.Problem("Bulkhead dolu: eşzamanlı istek sınırı aşıldı.", statusCode: 503, title: "Bulkhead Rejected", extensions: Meta());
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Results.Problem("HttpClient.Timeout aşıldı.", statusCode: 504, title: "Gateway Timeout", extensions: Meta());
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem($"Downstream'e ulaşılamadı: {ex.Message}", statusCode: 502, title: "Bad Gateway", extensions: Meta());
        }
    }

    // Fallback: hata ya da timeout → son bilinen fiyat / varsayılan. İstemci HER ZAMAN 200 alır, "source" alanına bakar.
    private static async Task<IResult> Fallback(Guid id, IHttpClientFactory factory, Polly.Registry.ResiliencePipelineProvider<string> pipelines,
        LastKnownPriceCache cache, CancellationToken ct)
    {
        var attempts = AttemptCounter.Begin();
        var sw = Stopwatch.StartNew();
        var pipeline = pipelines.GetPipeline<PriceResult>(FallbackPipeline);

        var context = ResilienceContextPool.Shared.Get(ct);
        context.Properties.Set(ProductIdKey, id);
        try
        {
            var result = await pipeline.ExecuteAsync(async ctx =>
            {
                using var response = await factory.CreateClient(FallbackUpstream).GetAsync($"/prices/{id}", ctx.CancellationToken);
                response.EnsureSuccessStatusCode(); // 4xx/5xx → exception → fallback
                var price = (await response.Content.ReadFromJsonAsync<PriceDto>(ctx.CancellationToken))!;
                cache.Set(price);
                return new PriceResult(price, "live");
            }, context);

            return Results.Ok(new
            {
                policy = "fallback",
                source = result.Source,
                result.FallbackKind,
                result.FallbackReason,
                upstreamAttempts = attempts.Value,
                elapsedMs = sw.ElapsedMilliseconds,
                price = result.Price
            });
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }
}
