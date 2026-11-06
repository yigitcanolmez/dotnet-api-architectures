using Layered.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Layered.DataAccess;

// [Data Access Katmanı] EF Core DbContext. Entity'ler doğrudan tablo olarak eşleniyor.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.Status).HasConversion<string>();
            b.HasMany(o => o.Lines).WithOne(l => l.Order).HasForeignKey(l => l.OrderId);
        });

        modelBuilder.Entity<Product>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).HasMaxLength(200);
            b.HasData(SeedData.Products);
        });
    }
}
