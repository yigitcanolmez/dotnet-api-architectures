// [LEGACY SİSTEM] "Eski sistem" simülasyonu. Bilinçli olarak legacy stilde yazıldı:
// katman yok, iş kuralları controller'da, ham SQL, string tutarlar, kısaltma kolon adları.
// Strangler Fig'de bu uygulamaya DOKUNMADAN önüne bir proxy koyup özellikleri tek tek
// Modern.Api'ye taşıyoruz. Taşınmamış özellikler (ör. /api/customers, /api/products) burada kalır.
using Legacy.Api;

var builder = WebApplication.CreateBuilder(args);
// Legacy JSON alan adları (ORD_NO, STAT_CD...) olduğu gibi kalsın diye camelCase kapatıldı.
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null);

var app = builder.Build();

LegacyDb.CONN_STR = app.Configuration.GetConnectionString("LegacyDb")!;
LegacyDb.INIT_DB();

app.MapControllers();
app.MapGet("/", () => "LEGACY ORDER SYSTEM v2.3 (2009) - OK");

app.Run();
