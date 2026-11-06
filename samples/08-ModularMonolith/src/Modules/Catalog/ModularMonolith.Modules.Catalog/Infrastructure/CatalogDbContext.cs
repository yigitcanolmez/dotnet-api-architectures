using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Catalog.Domain;
using ModularMonolith.Shared.Infrastructure.Persistence;

namespace ModularMonolith.Modules.Catalog.Infrastructure;

// [Catalog — Veri izolasyonu] Sadece Catalog modülü bu DbContext'i (catalog.db) kullanır.
// Üretimde: aynı veritabanı + HasDefaultSchema("catalog"). Başka modülün tablosuna JOIN YASAK.
internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : ModuleDbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<StockReservation>(b =>
        {
            b.HasKey(r => r.OrderId);
            b.Property(r => r.Status).HasConversion<string>();
            b.OwnsMany(r => r.Lines, l =>
            {
                l.WithOwner().HasForeignKey("OrderId");
                l.Property<int>("Id");
                l.HasKey("Id");
            });
            b.Navigation(r => r.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
