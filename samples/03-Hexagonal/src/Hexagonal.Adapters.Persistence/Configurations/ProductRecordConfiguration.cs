using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hexagonal.Adapters.Persistence.Configurations;

public class ProductRecordConfiguration : IEntityTypeConfiguration<ProductRecord>
{
    public void Configure(EntityTypeBuilder<ProductRecord> b)
    {
        b.ToTable("Products");
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).HasMaxLength(200);
        b.HasData(
            new ProductRecord { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Klavye", Price = 750.00m, Stock = 10 },
            new ProductRecord { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Mouse", Price = 350.00m, Stock = 20 },
            new ProductRecord { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Monitör", Price = 6500.00m, Stock = 5 });
    }
}
