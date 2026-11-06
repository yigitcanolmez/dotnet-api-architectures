using Hexagonal.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hexagonal.Adapters.Persistence.Configurations;

// Domain'i kirletmeden eşleme: owned collection + _lines backing field.
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders");
        b.HasKey(o => o.Id);
        b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(o => o.PaymentTransactionId).HasMaxLength(100);
        b.Ignore(o => o.Total);

        b.OwnsMany(o => o.Lines, line =>
        {
            line.ToTable("OrderLines");
            line.WithOwner().HasForeignKey("OrderId");
            line.Property<int>("Id");
            line.HasKey("Id");
            line.Property(l => l.ProductName).HasMaxLength(200);
            line.Ignore(l => l.LineTotal);
        });

        b.Navigation(o => o.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
