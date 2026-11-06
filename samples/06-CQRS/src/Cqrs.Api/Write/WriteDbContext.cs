using Cqrs.Api.Write.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cqrs.Api.Write;

// [Write] Normalize yazma modeli: Orders, OrderLines, Products. Sadece komut handler'ları kullanır.
// SaveChanges başarılı olduktan sonra aggregate'lerin domain event'leri yayınlanır.
public sealed class WriteDbContext(DbContextOptions<WriteDbContext> options, IEventPublisher publisher)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var aggregates = ChangeTracker.Entries<Order>().Select(e => e.Entity)
            .Where(o => o.DomainEvents.Count > 0).ToList();
        var events = aggregates.SelectMany(o => o.DomainEvents).ToList();

        var result = await base.SaveChangesAsync(ct);

        // ⚠ Burada DB commit'i ile event yayını ARASINDA process çökerse event kaybolur.
        // Üretimde Outbox deseni kullanılır: event'ler aynı transaction'da Outbox tablosuna yazılır.
        aggregates.ForEach(o => o.ClearDomainEvents());
        foreach (var domainEvent in events)
            await publisher.PublishAsync(domainEvent, ct);

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("Products");
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("Orders");
            b.HasKey(o => o.Id);
            b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            b.Ignore(o => o.DomainEvents);
            b.OwnsMany(o => o.Lines, l =>
            {
                l.ToTable("OrderLines");
                l.WithOwner().HasForeignKey("OrderId");
                l.Property<int>("Id");
                l.HasKey("Id");
                l.Property(x => x.ProductName).HasMaxLength(200);
            });
            b.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
