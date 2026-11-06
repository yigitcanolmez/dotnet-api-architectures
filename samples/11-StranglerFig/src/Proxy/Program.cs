// [PROXY / FACADE] İstemciler SADECE bu adresi (5200) bilir. Arkada legacy mi modern mi çalıştığı gizlidir.
// Route tablosu appsettings'teki Strangler:OrdersMigrated flag'inden üretilir ve dosya değişince
// uygulama yeniden başlatılmadan güncellenir (IOptionsMonitor + InMemoryConfigProvider.Update).
using Microsoft.Extensions.Options;
using Proxy;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

var section = builder.Configuration.GetSection("Strangler");
builder.Services.Configure<StranglerOptions>(section);
var initial = section.Get<StranglerOptions>() ?? new StranglerOptions();

builder.Services.AddReverseProxy()
    .LoadFromMemory(StranglerRouteTable.BuildRoutes(initial), StranglerRouteTable.BuildClusters(initial));

var app = builder.Build();

var proxyConfig = app.Services.GetRequiredService<InMemoryConfigProvider>();
app.Services.GetRequiredService<IOptionsMonitor<StranglerOptions>>().OnChange(o =>
{
    proxyConfig.Update(StranglerRouteTable.BuildRoutes(o), StranglerRouteTable.BuildClusters(o));
    app.Logger.LogWarning("Strangler route tablosu yeniden yüklendi: OrdersMigrated={Flag} → /api/orders/** {Target}",
        o.OrdersMigrated, o.OrdersMigrated ? "MODERN" : "LEGACY");
});

// Teşhis: proxy'nin şu anki yönlendirme tablosu (YARP route'larından önce eşleşir — literal path).
app.MapGet("/_strangler", (IOptionsMonitor<StranglerOptions> o) => new
{
    o.CurrentValue.OrdersMigrated,
    routes = proxyConfig.GetConfig().Routes.Select(r => new { r.RouteId, r.Match.Path, r.ClusterId, r.Order })
});

app.MapReverseProxy();

app.Run();
