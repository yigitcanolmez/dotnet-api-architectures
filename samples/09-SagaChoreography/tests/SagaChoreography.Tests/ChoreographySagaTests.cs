using System.Net;
using System.Net.Http.Json;

namespace SagaChoreography.Tests;

// Saga'nın üç senaryosu uçtan uca: HTTP ile sipariş ver, olaylar servisler arasında dolaşırken
// GET /orders/{id} ile sonucu POLLING yaparak bekle (eventual consistency).
public class ChoreographySagaTests(SagaFactory factory) : IClassFixture<SagaFactory>
{
    private static readonly Guid Keyboard = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Mouse = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Monitor = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task A_one_keyboard_happy_path_order_is_confirmed()
    {
        var stockBefore = await GetStockAsync(Keyboard);

        var orderId = await PlaceOrderAsync(Keyboard, 1);
        var order = await WaitForFinalStatusAsync(orderId);

        Assert.Equal("Confirmed", order.Status);
        Assert.Equal(stockBefore - 1, await GetStockAsync(Keyboard));
        var payment = (await GetPaymentsAsync()).Single(p => p.OrderId == orderId);
        Assert.Equal("Completed", payment.Status);
        Assert.Equal(750m, payment.Amount);
    }

    [Fact]
    public async Task B_two_monitors_payment_fails_stock_is_compensated_and_order_rejected()
    {
        var stockBefore = await GetStockAsync(Monitor);

        var orderId = await PlaceOrderAsync(Monitor, 2); // 13.000 TRY > 10.000 limit
        var order = await WaitForFinalStatusAsync(orderId);

        Assert.Equal("Rejected", order.Status);
        Assert.Contains("Limit", order.StatusReason);
        Assert.Equal("Failed", (await GetPaymentsAsync()).Single(p => p.OrderId == orderId).Status);

        // COMPENSATION: Inventory, PaymentFailed'ı dinleyip rezervasyonu serbest bıraktı.
        await EventuallyAsync(async () => await GetStockAsync(Monitor) == stockBefore);
    }

    [Fact]
    public async Task C_fifty_keyboards_out_of_stock_order_rejected_and_no_payment_attempted()
    {
        var stockBefore = await GetStockAsync(Keyboard);

        var orderId = await PlaceOrderAsync(Keyboard, 50);
        var order = await WaitForFinalStatusAsync(orderId);

        Assert.Equal("Rejected", order.Status);
        Assert.Contains("stok", order.StatusReason);
        Assert.Equal(stockBefore, await GetStockAsync(Keyboard));
        Assert.DoesNotContain(await GetPaymentsAsync(), p => p.OrderId == orderId);
    }

    [Fact]
    public async Task Cancelling_a_confirmed_order_refunds_payment_and_releases_stock_shipped_order_returns_409()
    {
        var stockBefore = await GetStockAsync(Mouse);
        var orderId = await PlaceOrderAsync(Mouse, 2);
        Assert.Equal("Confirmed", (await WaitForFinalStatusAsync(orderId)).Status);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"/orders/{orderId}/cancel", null)).StatusCode);

        await EventuallyAsync(async () => await GetStockAsync(Mouse) == stockBefore);
        await EventuallyAsync(async () => (await GetPaymentsAsync()).Single(p => p.OrderId == orderId).Status == "Refunded");

        var shippedId = await PlaceOrderAsync(Mouse, 1);
        Assert.Equal("Confirmed", (await WaitForFinalStatusAsync(shippedId)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"/orders/{shippedId}/ship", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/orders/{shippedId}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Invalid_requests_return_problem_details()
    {
        var unknown = await _client.PostAsJsonAsync("/orders", new
        {
            customerId = Guid.NewGuid(),
            items = new[] { new { productId = Guid.NewGuid(), quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/orders/{Guid.NewGuid()}")).StatusCode);
    }

    private async Task<Guid> PlaceOrderAsync(Guid productId, int quantity)
    {
        var response = await _client.PostAsJsonAsync("/orders", new
        {
            customerId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            items = new[] { new { productId, quantity } }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedDto>())!.Id;
    }

    private async Task<OrderDto> WaitForFinalStatusAsync(Guid orderId)
    {
        OrderDto? order = null;
        await EventuallyAsync(async () =>
        {
            order = await _client.GetFromJsonAsync<OrderDto>($"/orders/{orderId}");
            return order!.Status != "Pending";
        });
        return order!;
    }

    private async Task<int> GetStockAsync(Guid productId) =>
        (await _client.GetFromJsonAsync<InventoryDto>("/inventory"))!.Stocks.Single(s => s.ProductId == productId).Available;

    private async Task<List<PaymentDto>> GetPaymentsAsync() =>
        (await _client.GetFromJsonAsync<List<PaymentDto>>("/payments"))!;

    private static async Task EventuallyAsync(Func<Task<bool>> condition, int timeoutSeconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(200);
        }
        throw new TimeoutException($"Beklenen durum {timeoutSeconds} sn içinde oluşmadı.");
    }

    private sealed record CreatedDto(Guid Id);
    private sealed record OrderDto(Guid Id, string Status, string? StatusReason, decimal Total);
    private sealed record StockDto(Guid ProductId, string ProductName, int Available);
    private sealed record InventoryDto(List<StockDto> Stocks);
    private sealed record PaymentDto(Guid Id, Guid OrderId, decimal Amount, string Status, string? FailureReason);
}
