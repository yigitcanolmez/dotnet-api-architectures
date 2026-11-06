using Clean.Application.Products.Queries.ListProducts;
using MediatR;

namespace Clean.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListProductsQuery(), ct)))
            .WithTags("Products");

        return app;
    }
}
