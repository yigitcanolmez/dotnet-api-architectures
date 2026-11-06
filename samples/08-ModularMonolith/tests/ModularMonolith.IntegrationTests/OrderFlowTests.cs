using System.Net;
using System.Net.Http.Json;

namespace ModularMonolith.IntegrationTests;

// Modüller arası ASENKRON akışın uçtan uca testi: Ordering → (outbox) → in-memory bus → Catalog + Shipping.
// Sonuçlar "eventually consistent" olduğu için polling ile beklenir.
public class OrderFlowTests(ModularMonolithFactory factory) : IClassFixture<ModularMonolithFactory>
{
    private static readonly Guid Keyboard = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Mouse = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Monitor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Customer = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Placing_an_order_eventually_creates_a_shipment_and_decreases_stock()
    {
        var stockBefore = await GetStockAsync(Mouse);

        var orderId = await PlaceOrderAsync(Mouse, 3);

        var shipment = await EventuallyAsync(async () =>
        {
            var response = await _client.GetAsync($"/shipments/by-order/{orderId}");
            return response.StatusCode == HttpStatusCode.OK ? await response.Content.ReadFromJsonAsync<ShipmentDto>() : null;
        });
        Assert.Equal("AwaitingShipment", shipment.Status);
        Assert.Equal(3, shipment.ItemCount);

        await EventuallyAsync(async () => await GetStockAsync(Mouse) == stockBefore - 3 ? true : (bool?)null);
    }

    [Fact]
    public async Task Cancelling_an_order_eventually_cancels_shipment_and_restores_stock()
    {
        var stockBefore = await GetStockAsync(Keyboard);
        var orderId = await PlaceOrderAsync(Keyboard, 2);
        await EventuallyAsync(async () => await GetStockAsync(Keyboard) == stockBefore - 2 ? true : (bool?)null);

        var cancel = await _client.PostAsync($"/orders/{orderId}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        await EventuallyAsync(async () => await GetStockAsync(Keyboard) == stockBefore ? true : (bool?)null);
        var shipment = await EventuallyAsync(async () =>
        {
            var s = await _client.GetFromJsonAsync<ShipmentDto>($"/shipments/by-order/{orderId}");
            return s!.Status == "Cancelled" ? s : null;
        });
        Assert.Equal("Cancelled", shipment.Status);
    }

    [Fact]
    public async Task Shipped_order_cannot_be_cancelled_and_shipment_becomes_shipped()
    {
        var orderId = await PlaceOrderAsync(Monitor, 1);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"/orders/{orderId}/ship", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/orders/{orderId}/cancel", null)).StatusCode);

        var order = await _client.GetFromJsonAsync<OrderDto>($"/orders/{orderId}");
        Assert.Equal("Shipped", order!.Status);

        await EventuallyAsync(async () =>
        {
            var response = await _client.GetAsync($"/shipments/by-order/{orderId}");
            if (response.StatusCode != HttpStatusCode.OK) return null;
            var s = await response.Content.ReadFromJsonAsync<ShipmentDto>();
            return s!.Status == "Shipped" ? s : null;
        });
    }

    [Fact]
    public async Task Unknown_product_returns_400_and_missing_order_returns_404()
    {
        var response = await _client.PostAsJsonAsync("/orders", new
        {
            customerId = Customer,
            items = new[] { new { productId = Guid.NewGuid(), quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/orders/{Guid.NewGuid()}")).StatusCode);
    }

    private async Task<Guid> PlaceOrderAsync(Guid productId, int quantity)
    {
        var response = await _client.PostAsJsonAsync("/orders", new
        {
            customerId = Customer,
            items = new[] { new { productId, quantity } }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedDto>())!.Id;
    }

    private async Task<int> GetStockAsync(Guid productId)
    {
        var products = await _client.GetFromJsonAsync<List<ProductDto>>("/products");
        return products!.Single(p => p.Id == productId).Stock;
    }

    private static async Task<T> EventuallyAsync<T>(Func<Task<T?>> probe, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var value = await probe();
            if (value is not null) return value;
            await Task.Delay(200);
        }
        throw new TimeoutException($"Beklenen durum {timeoutSeconds} sn içinde oluşmadı.");
    }

    private sealed record CreatedDto(Guid Id);
    private sealed record ProductDto(Guid Id, string Name, decimal Price, int Stock);
    private sealed record OrderDto(Guid Id, string Status, decimal Total);
    private sealed record ShipmentDto(Guid Id, Guid OrderId, int ItemCount, string Status);
}
