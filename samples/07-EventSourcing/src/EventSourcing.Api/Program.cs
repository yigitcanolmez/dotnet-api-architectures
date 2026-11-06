using EventSourcing.Api.Accounts;
using EventSourcing.Api.Common;
using EventSourcing.Api.EventStore;
using EventSourcing.Api.Projections;
using EventSourcing.Api.Snapshots;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default")!;

// ── Yazma tarafı: event store (tek doğruluk kaynağı) + snapshot (optimizasyon)
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(EventTypeRegistry.CreateDefault());
builder.Services.AddSingleton(sp => new SqliteEventStore(
    connectionString, sp.GetRequiredService<EventTypeRegistry>(), sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<IEventStore>(sp => sp.GetRequiredService<SqliteEventStore>());
builder.Services.AddSingleton(new SnapshotStore(connectionString));
builder.Services.AddScoped<BankAccountRepository>();

// ── Okuma tarafı: event'lerden türetilen projeksiyon + catch-up worker
builder.Services.AddSingleton(sp => new AccountBalanceProjection(
    connectionString, sp.GetRequiredService<IEventStore>(), sp.GetRequiredService<ILogger<AccountBalanceProjection>>()));
builder.Services.AddHostedService<ProjectionWorker>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Öğretim amaçlı sadelik: migration yok, tablolar CREATE TABLE IF NOT EXISTS ile oluşturulur.
app.Services.GetRequiredService<SqliteEventStore>().EnsureSchema();
app.Services.GetRequiredService<SnapshotStore>().EnsureSchema();
app.Services.GetRequiredService<AccountBalanceProjection>().EnsureSchema();

app.MapAccountEndpoints();

app.Run();

public partial class Program;
