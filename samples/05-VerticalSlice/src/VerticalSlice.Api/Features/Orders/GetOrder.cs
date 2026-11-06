using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Infrastructure;

namespace VerticalSlice.Api.Features.Orders;

// [Dilim: Sipariş Getir] Okuma dilimi: domain nesnesi YOK, AsNoTracking + doğrudan DTO projeksiyonu.
// Her dilim kendi ihtiyacı kadar karmaşıklık seçer.
public static class GetOrder
{
    public sealed record Response(
        Guid Id, Guid CustomerId, string Status, decimal Total, DateTime CreatedAt, List<LineResponse> Lines);
    public sealed record LineResponse(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public Task<Response?> HandleAsync(Guid id, CancellationToken ct) =>
            db.Orders.AsNoTracking()
                .Where(o => o.Id == id)
                .Select(o => new Response(o.Id, o.CustomerId, o.Status.ToString(), o.Total, o.CreatedAt,
                    o.Lines.Select(l => new LineResponse(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice)).ToList()))
                .FirstOrDefaultAsync(ct);
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/orders/{id:guid}", async (Guid id, Handler handler, CancellationToken ct) =>
                    await handler.HandleAsync(id, ct) is { } order
                        ? Results.Ok(order)
                        : Error.NotFound($"Sipariş bulunamadı: {id}").ToProblem())
                .WithTags("Orders")
                .WithName(nameof(GetOrder));
    }
}
