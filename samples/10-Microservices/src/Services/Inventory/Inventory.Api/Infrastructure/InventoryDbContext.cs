// [Inventory — Infrastructure] Inventory'nin KENDİ veritabanı (inventory.db).
// InboxState tablosu: işlenen mesaj ID'leri iş verisiyle AYNI transaction'da saklanır (idempotent consumer).
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Inventory.Api.Domain;

namespace Inventory.Api.Infrastructure;

public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<Reservation>(b =>
        {
            b.HasKey(r => r.Id);
            b.HasIndex(r => r.OrderId).IsUnique();
            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            b.OwnsMany(r => r.Items, i =>
            {
                i.WithOwner().HasForeignKey("ReservationId");
                i.Property<int>("Id");
                i.HasKey("Id");
                i.ToTable("ReservationItems");
            });
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
