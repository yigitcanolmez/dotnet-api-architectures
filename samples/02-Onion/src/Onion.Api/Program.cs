using Onion.Api.Endpoints;
using Onion.Api.ErrorHandling;
using Onion.Application.Orders;
using Onion.Application.Products;
using Onion.Domain.Services;
using Onion.Infrastructure;

// [UI halkası + Composition Root] Soğanın tüm halkaları burada birbirine bağlanır.
// Api, Infrastructure'ı SADECE bu dosyada (DI kaydı için) kullanır.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionToProblemDetailsHandler>();

// Domain Services + Application Services
builder.Services.AddScoped<OrderPlacementService>();
builder.Services.AddScoped<OrderAppService>();
builder.Services.AddScoped<ProductAppService>();

// Infrastructure: arayüz → implementasyon eşlemeleri
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default eksik."));

var app = builder.Build();

app.Services.EnsureDatabaseCreated();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapProductEndpoints();
app.MapOrderEndpoints();

app.Run();
