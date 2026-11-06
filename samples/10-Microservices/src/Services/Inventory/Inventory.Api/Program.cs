// [Inventory Servisi — Composition Root] Port 5102, DB: inventory.db
// Sorumluluk: ürün stoğu ve sipariş rezervasyonları. Saga'nın compensatable adımı (ReserveStock / ReleaseStock).
using Contracts.Commands;
using Inventory.Api.Consumers;
using Inventory.Api.Endpoints;
using Inventory.Api.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<InventoryDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Inventory")));
builder.Services.AddScoped<StockReleaser>();

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    // Consumer tarafı: Inbox (dedupe) + Outbox (consumer içindeki Publish'ler DB ile atomik).
    x.AddEntityFrameworkOutbox<InventoryDbContext>(o =>
    {
        o.UseSqlite();
        o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
    });

    x.AddConfigureEndpointsCallback((context, _, cfg) =>
    {
        cfg.UseMessageRetry(r => r.Intervals(100, 500, 1000, 2000, 5000));
        cfg.UseEntityFrameworkOutbox<InventoryDbContext>(context);
    });

    // Komut kuyrukları sözleşmedeki adlarla sabitlenir (saga bu adreslere Send eder).
    x.AddConsumer<ReserveStockConsumer>().Endpoint(e => e.Name = QueueNames.ReserveStock);
    x.AddConsumer<ReleaseStockConsumer>().Endpoint(e => e.Name = QueueNames.ReleaseStock);
    x.AddConsumer<OrderCancelledConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbit = builder.Configuration.GetSection("RabbitMq");
        cfg.Host(rabbit["Host"] ?? "localhost", rabbit["VirtualHost"] ?? "/", h =>
        {
            h.Username(rabbit["Username"] ?? "guest");
            h.Password(rabbit["Password"] ?? "guest");
        });
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseExceptionHandler();
app.MapHealthChecks("/health");
app.MapInventoryEndpoints();

app.Run();
