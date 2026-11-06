// [Bağımlılık / "Downstream" servis] Kasıtlı olarak güvenilmez fiyat servisi.
// ResilienceClient.Api bu servisi farklı dayanıklılık politikalarıyla çağırır.
// GET /stats, gerçekte KAÇ istek geldiğini gösterir (retry/hedging'in ek yükünü görmek için).
using UnstableService.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<ChaosState>();

var app = builder.Build();

var prices = new Dictionary<Guid, (string Name, decimal Price)>
{
    [Guid.Parse("11111111-1111-1111-1111-111111111111")] = ("Klavye", 750.00m),
    [Guid.Parse("22222222-2222-2222-2222-222222222222")] = ("Mouse", 350.00m),
    [Guid.Parse("33333333-3333-3333-3333-333333333333")] = ("Monitör", 6500.00m)
};

app.MapGet("/", () => "UnstableService - GET /prices/{productId}, GET|POST /chaos, GET /stats");

app.MapGet("/prices/{productId:guid}", async (Guid productId, ChaosState chaos, HttpContext http, ILogger<Program> log) =>
{
    var requestNo = chaos.BeginRequest();
    bool? success = false;
    try
    {
        var s = chaos.Settings;
        if (s.Mode == "down")
            return Results.Problem("Servis kapalı (chaos: down).", statusCode: 503);

        if (s.LatencyMs > 0 && Random.Shared.NextDouble() < s.LatencyRate)
            await Task.Delay(s.LatencyMs, http.RequestAborted);

        if (Random.Shared.NextDouble() < s.FailureRate)
        {
            log.LogInformation("#{No} → 500 (chaos)", requestNo);
            return Results.Problem("Rastgele arıza (chaos: flaky).", statusCode: 500);
        }

        if (!prices.TryGetValue(productId, out var p))
        {
            success = true; // 404 bir arıza değil, geçerli bir iş yanıtı
            return Results.Problem($"Ürün bulunamadı: {productId}", statusCode: 404);
        }

        success = true;
        return Results.Ok(new { productId, name = p.Name, price = p.Price, currency = "TRY", requestNo, servedAt = DateTime.UtcNow });
    }
    catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
    {
        success = null; // istemci beklemekten vazgeçti (timeout / hedging kazananı belli oldu)
        return Results.Empty;
    }
    finally
    {
        chaos.EndRequest(success);
    }
});

app.MapGet("/chaos", (ChaosState chaos) => chaos.Settings);

app.MapPost("/chaos", (ChaosRequest req, ChaosState chaos, ILogger<Program> log) =>
{
    try
    {
        var s = chaos.Apply(req);
        log.LogWarning("CHAOS ayarlandı: {@Settings}", s);
        return Results.Ok(s);
    }
    catch (ArgumentException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
});

app.MapGet("/stats", (ChaosState chaos) => chaos.Stats());
app.MapPost("/stats/reset", (ChaosState chaos) => { chaos.Reset(); return Results.NoContent(); });

app.Run();
