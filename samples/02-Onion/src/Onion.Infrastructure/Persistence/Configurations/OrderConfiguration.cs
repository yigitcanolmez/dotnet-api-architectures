using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onion.Domain.Entities;

namespace Onion.Infrastructure.Persistence.Configurations;

// [Infrastructure halkası] Order'ın veritabanı eşlemesi Domain'in DIŞINDA.
// Domain'deki private List<OrderLine> _lines alanına EF "backing field" ile erişir.
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders");
        b.HasKey(o => o.Id);
        b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        b.Ignore(o => o.Total); // hesaplanan değer, kolon değil

        // OrderLine, Order aggregate'inin parçası → owned collection (kendi tablosu, gölge anahtar/FK).
        b.OwnsMany(o => o.Lines, line =>
        {
            line.ToTable("OrderLines");
            line.WithOwner().HasForeignKey("OrderId");
            line.Property<int>("Id");
            line.HasKey("Id");
            line.Property(l => l.ProductName).HasMaxLength(200);
            line.Ignore(l => l.LineTotal);
        });

        b.Navigation(o => o.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
