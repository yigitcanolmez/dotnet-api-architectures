// [Payment — Infrastructure] Payment'ın KENDİ veritabanı (payment.db) + MassTransit inbox/outbox tabloları.
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Payment.Api.Infrastructure;

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Payment> Payments => Set<Domain.Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Payment>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasIndex(p => p.OrderId).IsUnique();
            b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
