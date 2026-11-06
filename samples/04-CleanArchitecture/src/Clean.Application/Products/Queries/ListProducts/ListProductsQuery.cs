using MediatR;

namespace Clean.Application.Products.Queries.ListProducts;

public sealed record ListProductsQuery : IRequest<IReadOnlyList<ProductDto>>;

public sealed record ProductDto(Guid Id, string Name, decimal Price, string Currency, int Stock);
