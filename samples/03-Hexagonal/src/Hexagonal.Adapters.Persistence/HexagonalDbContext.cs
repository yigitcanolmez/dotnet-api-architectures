using Hexagonal.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hexagonal.Adapters.Persistence;

// [Driven Adapter: Persistence] EF Core sadece bu projede.
public class HexagonalDbContext(DbContextOptions<HexagonalDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<ProductRecord> Products => Set<ProductRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HexagonalDbContext).Assembly);
}
