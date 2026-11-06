using MassTransit;
using Microsoft.EntityFrameworkCore;
using SagaChoreography.BuildingBlocks;

namespace SagaChoreography.Services.Ordering;

// [Ordering Servisi — Veri] ordering.db. Diğer servislerin tablolarını GÖREMEZ.
// MassTransit EF Outbox tabloları (OutboxMessage/OutboxState/InboxState) da burada: POST /orders,
// siparişi ve OrderCreated mesajını TEK transaction'da yazar (dual-write problemi yok).
public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options), IInboxDbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<CatalogItem> Catalog => Set<CatalogItem>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
            });
            b.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        modelBuilder.Entity<CatalogItem>().HasKey(c => c.Id);
        modelBuilder.ConfigureInbox();

        // MassTransit transactional outbox tabloları
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    public async Task SeedAsync()
    {
        if (await Catalog.AnyAsync()) return;
        Catalog.AddRange(
            new CatalogItem(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Klavye", 750.00m),
            new CatalogItem(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Mouse", 350.00m),
            new CatalogItem(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Monitör", 6500.00m));
        await SaveChangesAsync();
    }
}
