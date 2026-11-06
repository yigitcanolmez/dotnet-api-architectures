// [MODERN SİSTEM] Orders özelliğinin temiz, rich domain + Vertical Slice ile yeniden yazılmış hali.
// Proxy, Strangler:OrdersMigrated=true iken /api/orders/** trafiğini buraya yollar.
using Microsoft.AspNetCore.Diagnostics;
using Modern.Api.AntiCorruption;
using Modern.Api.Data;
using Modern.Api.Domain;
using Modern.Api.Features.AclDemo;
using Modern.Api.Features.Orders;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddDbContext<ModernDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("ModernDb")));

// ACL: domain portu (IProductCatalog) → legacy HTTP API'sini çağıran çevirmen.
builder.Services.AddHttpClient<IProductCatalog, LegacyProductTranslator>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Legacy:BaseUrl"]!);
    c.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<ModernDbContext>().Database.EnsureCreated(); // öğretim amaçlı: migration yok

app.UseExceptionHandler(e => e.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, detail) = ex is DomainException de
        ? (de.IsConflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest, de.Message)
        : (StatusCodes.Status500InternalServerError, "Beklenmeyen hata.");
    await Results.Problem(detail, statusCode: status).ExecuteAsync(ctx);
}));

app.MapGet("/", () => "Modern Orders API (.NET 10) - OK");
PlaceOrder.Map(app);
ListOrders.Map(app);
GetOrder.Map(app);
ShipOrder.Map(app);
CancelOrder.Map(app);
ListProductsViaAcl.Map(app);

app.Run();

public partial class Program;
