using Microsoft.EntityFrameworkCore;
using SagaChoreography.BuildingBlocks;

namespace SagaChoreography.Services.Payment;

// [Payment Servisi — Veri] payment.db
public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options), IInboxDbContext
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasIndex(p => p.OrderId).IsUnique(); // ikinci güvence: sipariş başına tek ödeme (unique constraint idempotency)
            b.Property(p => p.Status).HasConversion<string>();
        });
        modelBuilder.ConfigureInbox();
    }
}
