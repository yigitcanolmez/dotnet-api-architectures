using System.Text.Json.Serialization;
using Layered.Api.Middleware;
using Layered.Business.Services;
using Layered.DataAccess;
using Layered.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

// Katmanlı mimaride herkes somut sınıfları kaydeder; interface yok.
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<OrderRepository>();
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ProductService>();

var app = builder.Build();

// Öğretim amaçlı sadelik: migration yerine EnsureCreated + HasData seed.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages();
app.MapControllers();

app.Run();
