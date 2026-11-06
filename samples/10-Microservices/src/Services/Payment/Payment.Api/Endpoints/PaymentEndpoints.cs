// [Payment — API] Sadece okuma.
using Microsoft.EntityFrameworkCore;
using Payment.Api.Infrastructure;

namespace Payment.Api.Endpoints;

public record PaymentDto(Guid Id, Guid OrderId, Guid CustomerId, decimal Amount, string Status, string? FailureReason, DateTime ProcessedAt);

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        // ?orderId=... ile filtrelenebilir (Gateway'deki API Composition bunu kullanır).
        app.MapGet("/payments", async (Guid? orderId, PaymentDbContext db, CancellationToken ct) =>
        {
            var query = db.Payments.AsNoTracking();
            if (orderId is not null) query = query.Where(p => p.OrderId == orderId);
            return (await query.ToListAsync(ct))
                .OrderByDescending(p => p.ProcessedAt)
                .Select(p => new PaymentDto(p.Id, p.OrderId, p.CustomerId, p.Amount, p.Status.ToString(), p.FailureReason, p.ProcessedAt));
        });
    }
}
