using Clean.Application.Abstractions;
using Clean.Domain.Orders;
using Clean.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Clean.Infrastructure.Persistence;

// [Infrastructure Katmanı – Frameworks & Drivers] Application'ın IApplicationDbContext portunun EF Core adaptörü.
// Mapping detayları (tablo, kolon, dönüşüm) Configurations/ altında; Domain bunlardan habersiz.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
