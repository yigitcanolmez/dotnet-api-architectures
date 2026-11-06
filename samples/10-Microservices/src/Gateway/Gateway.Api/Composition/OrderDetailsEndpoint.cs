// [Gateway — API Composition] Tek ekran için 3 servisten veriyi PARALEL toplayıp birleştirir.
// İstemci 4 çağrı yerine 1 çağrı yapar. Gateway'de İŞ KURALI YOK; sadece birleştirme (aptal kalmalı).
using System.Text.Json;

namespace Gateway.Api.Composition;

public record OrderDetailsResponse(
    JsonElement Order,
    JsonElement? Saga,
    JsonElement? Payments,
    JsonElement? Reservations,
    IReadOnlyList<string> Warnings);

public static class OrderDetailsEndpoint
{
    public static void MapOrderDetails(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/order-details/{id:guid}", async (Guid id, OrderingClient ordering, PaymentClient payment,
            InventoryClient inventory, CancellationToken ct) =>
        {
            var orderTask = ordering.GetOrderAsync(id, ct);
            var sagaTask = ordering.GetSagaAsync(id, ct);
            var paymentsTask = payment.GetPaymentsAsync(id, ct);
            var reservationsTask = inventory.GetReservationsAsync(id, ct);

            await Task.WhenAll(orderTask, sagaTask, paymentsTask, reservationsTask);

            var order = orderTask.Result;
            if (order.IsNotFound) return Results.Problem(statusCode: 404, title: "Sipariş bulunamadı");
            if (!order.IsSuccess)
                return Results.Problem(statusCode: 503, title: "Ordering servisine ulaşılamıyor", detail: order.Error);

            // Kısmi yanıt (graceful degradation): yardımcı veriler gelmezse uyarıyla devam et.
            var warnings = new[] { sagaTask.Result, paymentsTask.Result, reservationsTask.Result }
                .Where(r => !r.IsSuccess && !r.IsNotFound)
                .Select(r => r.Error!)
                .ToList();

            return Results.Ok(new OrderDetailsResponse(
                order.Body!.Value,
                sagaTask.Result.Body,
                paymentsTask.Result.Body,
                reservationsTask.Result.Body,
                warnings));
        }).RequireRateLimiting("fixed");
    }
}
