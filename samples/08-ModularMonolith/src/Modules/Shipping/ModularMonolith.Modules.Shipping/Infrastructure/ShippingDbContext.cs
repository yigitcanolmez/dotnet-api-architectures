using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Shipping.Domain;
using ModularMonolith.Shared.Infrastructure.Persistence;

namespace ModularMonolith.Modules.Shipping.Infrastructure;

// [Shipping — Veri izolasyonu] shipping.db (üretimde: HasDefaultSchema("shipping")).
internal sealed class ShippingDbContext(DbContextOptions<ShippingDbContext> options) : ModuleDbContext(options)
{
    public DbSet<Shipment> Shipments => Set<Shipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Shipment>(b =>
        {
            b.HasKey(s => s.Id);
            b.HasIndex(s => s.OrderId).IsUnique(); // sipariş başına tek kargo (ek idempotency güvencesi)
            b.Property(s => s.Status).HasConversion<string>();
        });
    }
}
