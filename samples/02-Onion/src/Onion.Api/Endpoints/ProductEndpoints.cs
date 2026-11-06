using Onion.Application.Products;

namespace Onion.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products", async (ProductAppService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct))).WithTags("Products");
        return app;
    }
}
