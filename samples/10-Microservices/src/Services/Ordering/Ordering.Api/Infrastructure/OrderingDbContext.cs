// [Ordering — Infrastructure] Ordering'in KENDİ veritabanı (ordering.db).
// Aynı DbContext üç işi birden taşır ve hepsi aynı SQLite transaction'ına girebilir:
//   1) İş verisi (Orders, CatalogItems)
//   2) Saga durumu (OrderSagas)               → MassTransit EF saga repository
//   3) Outbox/Inbox tabloları                 → MassTransit EF transactional outbox
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Domain;
using Ordering.Api.Saga;

namespace Ordering.Api.Infrastructure;

public class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<OrderState> OrderSagas => Set<OrderState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            b.OwnsMany(o => o.Lines, l =>
            {
                l.WithOwner().HasForeignKey("OrderId");
                l.Property<int>("Id");
                l.HasKey("Id");
                l.ToTable("OrderLines");
            });
            b.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CatalogItem>().HasKey(c => c.ProductId);

        new OrderStateMap().Configure(modelBuilder);

        // MassTransit outbox tabloları: InboxState, OutboxState, OutboxMessage
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    // Optimistic concurrency: saga satırı her güncellemede Version'ı artırır;
    // EF UPDATE ... WHERE Version = @eski üretir. Aynı saga'ya iki mesaj aynı anda gelirse
    // biri DbUpdateConcurrencyException alır ve MassTransit retry'ı onu taze veriyle tekrar işler.
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<OrderState>().Where(e => e.State == EntityState.Modified))
            entry.Entity.Version++;
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
