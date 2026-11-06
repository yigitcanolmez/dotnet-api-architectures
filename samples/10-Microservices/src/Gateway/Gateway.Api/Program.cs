// [API Gateway — Composition Root] Port 5100. İstemcilerin TEK giriş noktası.
// Sorumluluklar: yönlendirme (YARP), rate limiting, istek loglama, aktif health check, API composition.
// Gateway mesajlaşmaya katılmaz ve hiçbir servisin veritabanını görmez.
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Gateway.Api.Composition;
using Gateway.Api.Middleware;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
// Birleştirilmiş yanıtta Türkçe karakterler \uXXXX yerine olduğu gibi yazılsın.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All));

// 1) Reverse proxy: rotalar ve cluster'lar appsettings.json → "ReverseProxy" bölümünde.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// 2) Rate limiting (fixed window). YARP rotaları "RateLimiterPolicy": "fixed" ile bağlanır.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddFixedWindowLimiter("fixed", w =>
    {
        w.PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
        w.Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:WindowSeconds", 10));
        w.QueueLimit = 0;
    });
});

// 3) API Composition için typed HttpClient'lar + standart resilience pipeline.
// Adresler YARP cluster tanımından okunur → tek doğruluk kaynağı.
string ClusterAddress(string cluster) =>
    builder.Configuration[$"ReverseProxy:Clusters:{cluster}:Destinations:d1:Address"]
    ?? throw new InvalidOperationException($"'{cluster}' cluster adresi tanımlı değil.");

void ConfigureResilience(HttpStandardResilienceOptions o)
{
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);        // tek deneme üst sınırı
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);   // retry'lar dahil toplam üst sınır
    o.Retry.MaxRetryAttempts = 2;                              // exponential backoff + jitter (varsayılan)
    o.CircuitBreaker.FailureRatio = 0.5;
    o.CircuitBreaker.MinimumThroughput = 5;
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
}

builder.Services.AddHttpClient<OrderingClient>(c => c.BaseAddress = new Uri(ClusterAddress("ordering")))
    .AddStandardResilienceHandler(ConfigureResilience);
builder.Services.AddHttpClient<PaymentClient>(c => c.BaseAddress = new Uri(ClusterAddress("payment")))
    .AddStandardResilienceHandler(ConfigureResilience);
builder.Services.AddHttpClient<InventoryClient>(c => c.BaseAddress = new Uri(ClusterAddress("inventory")))
    .AddStandardResilienceHandler(ConfigureResilience);

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapOrderDetails();
app.MapReverseProxy();

app.Run();
