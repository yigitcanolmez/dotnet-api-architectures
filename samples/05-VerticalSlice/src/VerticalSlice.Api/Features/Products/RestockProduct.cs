using FluentValidation;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Infrastructure;

namespace VerticalSlice.Api.Features.Products;

// [Dilim: Stok Ekle] "Sonradan eklenen özellik" örneği: bu dosya eklendiğinde başka HİÇBİR
// dosya değişmedi (Program.cs dahil). Assembly taraması endpoint'i ve handler'ı kendisi bulur.
public static class RestockProduct
{
    public sealed record Request(int Quantity);
    public sealed record Response(Guid Id, int Stock);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1000);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<Response>> HandleAsync(Guid id, Request request, CancellationToken ct)
        {
            var product = await db.Products.FindAsync([id], ct);
            if (product is null)
                return Error.NotFound($"Ürün bulunamadı: {id}");

            var result = product.Restock(request.Quantity);
            if (!result.IsSuccess) return result.Error!;

            await db.SaveChangesAsync(ct);
            return new Response(product.Id, product.Stock);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/products/{id:guid}/restock", async (Guid id, Request request, Handler handler, CancellationToken ct) =>
                {
                    var result = await handler.HandleAsync(id, request, ct);
                    return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
                })
                .AddEndpointFilter<ValidationFilter<Request>>()
                .WithTags("Products")
                .WithName(nameof(RestockProduct));
    }
}
