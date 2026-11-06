using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onion.Domain.Entities;

namespace Onion.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products");
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).HasMaxLength(200);

        // Ortak seed (tüm örneklerde aynı GUID'ler)
        b.HasData(
            new Product(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Klavye", 750.00m, 10),
            new Product(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Mouse", 350.00m, 20),
            new Product(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Monitör", 6500.00m, 5));
    }
}
