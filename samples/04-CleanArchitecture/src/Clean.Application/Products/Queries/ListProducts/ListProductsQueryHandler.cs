using Clean.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Clean.Application.Products.Queries.ListProducts;

public sealed class ListProductsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListProductsQuery, IReadOnlyList<ProductDto>>
{
    public async Task<IReadOnlyList<ProductDto>> Handle(ListProductsQuery request, CancellationToken cancellationToken)
    {
        // SQLite decimal'i TEXT olarak sakladığı için SQL'de decimal sıralama yapılamaz; ada göre sıralıyoruz.
        return await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto(p.Id.Value, p.Name, p.Price.Amount, p.Price.Currency, p.Stock))
            .ToListAsync(cancellationToken);
    }
}
