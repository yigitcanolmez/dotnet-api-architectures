using Microsoft.EntityFrameworkCore;
using Onion.Domain.Entities;

namespace Onion.Infrastructure.Persistence;

// [Infrastructure halkası] EF Core burada yaşar. Domain entity'lerini kirletmeden
// eşlemeler IEntityTypeConfiguration sınıflarında yapılır (Configurations/ klasörü).
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
