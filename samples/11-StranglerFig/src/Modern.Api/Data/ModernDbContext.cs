using Microsoft.EntityFrameworkCore;
using Modern.Api.Domain;

namespace Modern.Api.Data;

// Modern sistemin KENDİ veritabanı (strangler-modern.db). Legacy DB'ye doğrudan erişim yok;
// legacy veriye ihtiyaç olunca ACL üzerinden legacy'nin API'si kullanılır.
public sealed class ModernDbContext(DbContextOptions<ModernDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Order>(o =>
        {
            o.HasKey(x => x.Id);
            o.Property(x => x.Status).HasConversion<string>();
            o.Property(x => x.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            o.Ignore(x => x.Total);
            o.OwnsMany(x => x.Lines, l =>
            {
                l.WithOwner().HasForeignKey("OrderId");
                l.Property<int>("Id");
                l.HasKey("Id");
                l.Ignore(x => x.LineTotal);
            });
            o.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
