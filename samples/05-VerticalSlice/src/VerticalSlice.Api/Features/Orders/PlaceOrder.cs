using FluentValidation;
using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Domain;
using VerticalSlice.Api.Infrastructure;

namespace VerticalSlice.Api.Features.Orders;

// [Dilim: Sipariş Ver] Request + Response + Validator + Handler + Endpoint TEK DOSYADA (REPR).
// "Sipariş verme" özelliğini değiştirmek isteyen geliştirici sadece bu dosyayı açar.
public static class PlaceOrder
{
    public sealed record Request(Guid CustomerId, List<RequestItem> Items);
    public sealed record RequestItem(Guid ProductId, int Quantity);
    public sealed record Response(Guid Id);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.CustomerId).NotEmpty();
            RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= Order.MaxLines)
                .WithMessage($"Sipariş en fazla {Order.MaxLines} kalem içerebilir.");
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId).NotEmpty();
                item.RuleFor(i => i.Quantity).GreaterThan(0);
            });
        }
    }

    // MediatR yok: Handler sıradan bir sınıf, DI'dan endpoint'e doğrudan enjekte edilir.
    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<Response>> HandleAsync(Request request, CancellationToken ct)
        {
            var ids = request.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

            var missing = ids.Where(id => !products.ContainsKey(id)).ToList();
            if (missing.Count > 0)
                return Error.Validation($"Bilinmeyen ürün: {string.Join(", ", missing)}");

            foreach (var item in request.Items)
            {
                var stock = products[item.ProductId].DecreaseStock(item.Quantity);
                if (!stock.IsSuccess) return stock.Error!;
            }

            var order = Order.Place(request.CustomerId,
                request.Items.Select(i => (products[i.ProductId], i.Quantity)).ToList());
            if (!order.IsSuccess) return order.Error!;

            db.Orders.Add(order.Value);
            await db.SaveChangesAsync(ct);
            return new Response(order.Value.Id);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/orders", async (Request request, Handler handler, CancellationToken ct) =>
                {
                    var result = await handler.HandleAsync(request, ct);
                    return result.IsSuccess
                        ? Results.Created($"/orders/{result.Value.Id}", result.Value)
                        : result.Error!.ToProblem();
                })
                .AddEndpointFilter<ValidationFilter<Request>>()
                .WithTags("Orders")
                .WithName(nameof(PlaceOrder));
    }
}
