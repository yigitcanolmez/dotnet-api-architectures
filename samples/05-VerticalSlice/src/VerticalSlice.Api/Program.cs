using FluentValidation;
using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Infrastructure;

// [Composition Root] Program.cs hiçbir özelliği (dilimi) tek tek bilmez.
// Yeni dilim = yeni dosya; buraya dokunmak gerekmez.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);
builder.Services.AddSlices(typeof(Program).Assembly);
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

await Seed.InitializeAsync(app.Services);

app.MapEndpoints();

app.Run();

public partial class Program;
