using Cqrs.Api.Common;
using Cqrs.Api.Read.Queries;

namespace Cqrs.Api.Endpoints;

// [API / Sorgu tarafı] Sadece okuma modelinden okur; WriteDbContext'e ve domain'e erişimi yok.
public static class QueryEndpoints
{
    public static IEndpointRouteBuilder MapQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/").WithTags("Queries");

        group.MapGet("/orders/{id:guid}", async (Guid id, IQueryHandler<GetOrderSummaryQuery, OrderSummaryDto?> handler, CancellationToken ct) =>
            await handler.HandleAsync(new GetOrderSummaryQuery(id), ct) is { } summary
                ? Results.Ok(summary)
                : Results.Problem(statusCode: 404, title: "NotFound",
                    detail: $"Sipariş okuma modelinde yok: {id} (henüz projekte edilmemiş olabilir)."));

        group.MapGet("/customers/{id:guid}/orders", async (Guid id, IQueryHandler<ListOrdersByCustomerQuery, IReadOnlyList<OrderSummaryDto>> handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListOrdersByCustomerQuery(id), ct)));

        group.MapGet("/customers/{id:guid}/stats", async (Guid id, IQueryHandler<GetCustomerStatsQuery, CustomerStatsDto> handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new GetCustomerStatsQuery(id), ct)));

        group.MapGet("/products", async (IQueryHandler<ListProductsQuery, IReadOnlyList<ProductDto>> handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListProductsQuery(), ct)));

        return app;
    }
}
