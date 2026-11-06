using Microsoft.EntityFrameworkCore;
using ModularMonolith.Shared.Abstractions.Events;
using ModularMonolith.Shared.Infrastructure.Inbox;
using ModularMonolith.Shared.Infrastructure.Outbox;

namespace ModularMonolith.Shared.Infrastructure.Persistence;

// [Shared.Infrastructure] Her modülün DbContext'i bundan türer. Böylece her modül KENDİ veritabanında
// (üretimde: aynı DB'de kendi schema'sında) OutboxMessages + InboxMessages tablolarını taşır.
// Outbox kaydı iş verisiyle AYNI SaveChanges (= aynı yerel transaction) içinde yazılır.
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    // Integration event'i doğrudan bus'a basmak yerine outbox'a ekler (dual-write problemine çözüm).
    public void AddIntegrationEvent(IIntegrationEvent integrationEvent) =>
        OutboxMessages.Add(OutboxMessage.From(integrationEvent));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Üretimde (SQL Server/PostgreSQL): modelBuilder.HasDefaultSchema("catalog") gibi modül başına schema.
        // SQLite schema desteklemediği için bu örnekte her modül AYRI bir .db dosyası kullanır.
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.ProcessedOnUtc, x.OccurredOnUtc });
        });

        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("InboxMessages");
            // Aynı event'i aynı handler ikinci kez işleyemez (unique key yarış durumunu da engeller).
            b.HasKey(x => new { x.EventId, x.HandlerName });
        });
    }
}
