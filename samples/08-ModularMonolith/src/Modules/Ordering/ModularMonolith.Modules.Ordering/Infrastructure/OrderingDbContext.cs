using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Ordering.Domain;
using ModularMonolith.Shared.Infrastructure.Persistence;

namespace ModularMonolith.Modules.Ordering.Infrastructure;

// [Ordering — Veri izolasyonu] ordering.db (üretimde: HasDefaultSchema("ordering")).
// Dikkat: Products tablosu YOK. Ürün adı/fiyatı sipariş anında ICatalogModuleApi'den alınıp
// OrderLine içine KOPYALANIR (sipariş, o anki fiyatın tarihsel kaydıdır).
internal sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : ModuleDbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.Status).HasConversion<string>();
            b.Ignore(o => o.Total);
            b.OwnsMany(o => o.Lines, l =>
            {
                l.WithOwner().HasForeignKey("OrderId");
                l.Property<int>("Id");
                l.HasKey("Id");
                l.Ignore(x => x.LineTotal);
            });
            b.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
