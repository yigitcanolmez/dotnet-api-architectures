using Clean.Domain.Orders;
using Clean.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clean.Infrastructure.Persistence.Configurations;

// Zengin domain modelini (private setter, backing field, VO, strongly-typed id) tabloya eşleyen yer.
// Domain'e [Key], [Column] gibi attribute koymak yerine tüm ORM bilgisi burada tutulur.
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasConversion(id => id.Value, value => new OrderId(value))   // strongly-typed id ↔ Guid
            .ValueGeneratedNever();

        builder.Property(o => o.CustomerId)
            .HasConversion(id => id.Value, value => new CustomerId(value));

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

        // SQLite DateTime.Kind bilgisini saklamaz; okurken UTC olarak işaretliyoruz.
        builder.Property(o => o.CreatedAtUtc)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(o => o.TrackingNumber).HasMaxLength(50);
        builder.Property(o => o.CancellationReason).HasMaxLength(200);

        // Money VO → complex type: Orders tablosunda Total_Amount + Total_Currency olarak iki kolon.
        builder.ComplexProperty(o => o.Total, money =>
        {
            money.Property(m => m.Amount).HasColumnName("TotalAmount").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName("TotalCurrency").HasMaxLength(3);
        });

        // Aggregate içi koleksiyon: public IReadOnlyCollection, EF ise private _lines alanını kullanır.
        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey("OrderId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude(); // aggregate her zaman bütün olarak yüklenir

        // DomainEvents kalıcı değildir.
        builder.Ignore(o => o.DomainEvents);
    }
}

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("OrderLines");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.ProductId)
            .HasConversion(id => id.Value, value => new ProductId(value));
        builder.Property(l => l.ProductName).HasMaxLength(200);

        builder.ComplexProperty(l => l.UnitPrice, money =>
        {
            money.Property(m => m.Amount).HasColumnName("UnitPriceAmount").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(3);
        });

        builder.Ignore(l => l.SubTotal);
    }
}
