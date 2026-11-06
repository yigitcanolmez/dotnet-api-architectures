using Clean.Domain.Orders;
using Clean.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Clean.Application.Abstractions;

// [Application Katmanı – Port] Use case'lerin kalıcılık ihtiyacını tarifleyen arayüz.
// Implementasyonu Infrastructure'da (AppDbContext). Bağımlılık TERSİNE çevrilmiştir:
// Application arayüzü TANIMLAR, Infrastructure UYGULAR.
//
// PRAGMATİK TAVİZ: DbSet<T> sunduğumuz için Application, EF Core paketini biliyor.
// Purist alternatif: IOrderRepository/IProductRepository + IUnitOfWork (bkz. README).
public interface IApplicationDbContext
{
    DbSet<Order> Orders { get; }
    DbSet<Product> Products { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
