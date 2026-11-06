using Microsoft.EntityFrameworkCore;
using SagaChoreography.BuildingBlocks;

namespace SagaChoreography.Services.Inventory;

// [Inventory Servisi — Veri] inventory.db. Ordering'in siparişlerini veya Payment'ın ödemelerini göremez.
public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options), IInboxDbContext
{
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Stock>(b =>
        {
            b.HasKey(s => s.ProductId);
            b.Property(s => s.Version).IsConcurrencyToken();
        });
        modelBuilder.Entity<Reservation>(b =>
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
        modelBuilder.ConfigureInbox();
    }

    public async Task SeedAsync()
    {
        if (await Stocks.AnyAsync()) return;
        Stocks.AddRange(
            new Stock(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Klavye", 10),
            new Stock(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Mouse", 20),
            new Stock(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Monitör", 5));
        await SaveChangesAsync();
    }
}
