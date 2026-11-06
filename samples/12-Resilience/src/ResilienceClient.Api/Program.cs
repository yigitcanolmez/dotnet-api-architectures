// [Çağıran servis] Aynı güvenilmez bağımlılığı (UnstableService, :5301) farklı dayanıklılık
// politikalarıyla çağıran endpoint'ler. Politikaların tanımı: Infrastructure/ResiliencePolicies.cs
using Microsoft.AspNetCore.RateLimiting;
using ResilienceClient.Api.Endpoints;
using ResilienceClient.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddPriceClients(builder.Configuration);

// GELEN istekler için rate limiting (sunucu tarafı koruma): token bucket — 5 jeton, 10 sn'de 5 jeton dolar.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddTokenBucketLimiter("token-bucket", x =>
    {
        x.TokenLimit = 5;
        x.TokensPerPeriod = 5;
        x.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
        x.QueueLimit = 0;
        x.AutoReplenishment = true;
    });
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "10";
        await Results.Problem("Çok fazla istek: token bucket boş. 10 sn sonra tekrar deneyin.", statusCode: 429)
            .ExecuteAsync(ctx.HttpContext);
    };
});

var app = builder.Build();

app.UseRateLimiter();

app.MapGet("/", () => "ResilienceClient - /naive /retry /timeout /circuit-breaker /standard /fallback /bulkhead /hedging /rate-limited");
app.MapPriceEndpoints();
app.MapGet("/rate-limited", () => Results.Ok(new { message = "Geçti ✅", at = DateTime.UtcNow }))
   .RequireRateLimiting("token-bucket");

app.Run();
