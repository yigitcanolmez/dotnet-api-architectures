// [Payment Servisi — Composition Root] Port 5103, DB: payment.db
// Sorumluluk: tahsilat. Saga'nın pivot adımı (ProcessPayment).
using Contracts.Commands;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Payment.Api.Consumers;
using Payment.Api.Endpoints;
using Payment.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<PaymentDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Payment")));

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.AddEntityFrameworkOutbox<PaymentDbContext>(o =>
    {
        o.UseSqlite();
        o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
    });

    x.AddConfigureEndpointsCallback((context, _, cfg) =>
    {
        cfg.UseMessageRetry(r => r.Intervals(100, 500, 1000, 2000, 5000));
        cfg.UseEntityFrameworkOutbox<PaymentDbContext>(context);
    });

    x.AddConsumer<ProcessPaymentConsumer>().Endpoint(e => e.Name = QueueNames.ProcessPayment);

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await db.Database.EnsureCreatedAsync(); // Öğretim amaçlı: migration yok.
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
}

app.UseExceptionHandler();
app.MapHealthChecks("/health");
app.MapPaymentEndpoints();

app.Run();
