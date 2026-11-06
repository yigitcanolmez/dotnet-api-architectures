using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Domain;

namespace VerticalSlice.Api.Infrastructure;

// [Infrastructure] Tüm dilimlerin paylaştığı tek DbContext. Repository soyutlaması YOK:
// dilimler DbContext'i doğrudan kullanır (EF Core zaten Unit of Work + Repository'dir).
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.Currency).HasMaxLength(3);
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(o => o.CustomerId);
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
