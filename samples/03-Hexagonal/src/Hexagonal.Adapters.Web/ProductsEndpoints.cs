using Hexagonal.Core.Ports.In;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexagonal.Adapters.Web;

public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products", async (IListProductsQuery query, CancellationToken ct) =>
            Results.Ok(await query.ListAsync(ct))).WithTags("Products");
        return app;
    }
}
