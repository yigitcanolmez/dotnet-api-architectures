using System.Net;
using System.Net.Http.Json;

namespace Cqrs.Tests;

// Komut → (kanal) → projeksiyon → sorgu akışını uçtan uca doğrular.
// Okuma modeli eventually consistent olduğu için sonuç POLLING ile beklenir.
public sealed class CommandToQueryFlowTests(CqrsApiFactory factory) : IClassFixture<CqrsApiFactory>
{
    private static readonly Guid Keyboard = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Mouse = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid MonitorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly HttpClient _client = factory.CreateClient();

    private sealed record IdResponse(Guid Id);
    private sealed record OrderSummary(Guid OrderId, Guid CustomerId, string Status, decimal Total, int ItemCount, string ProductNames);
    private sealed record CustomerStats(Guid CustomerId, int OrderCount, decimal TotalSpent);

    [Fact]
    public async Task Siparis_okuma_modeline_gecikmeli_yansir()
    {
        var customerId = Guid.NewGuid();

        var id = await PlaceOrderAsync(customerId, (Keyboard, 2), (Mouse, 1));

        // Hemen sonra: projektör (300ms gecikmeli) henüz çalışmadı → okuma modelinde yok.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/orders/{id}")).StatusCode);

        var summary = await EventuallyAsync(() => TryGetSummaryAsync(id), s => s is not null);
        Assert.Equal("Placed", summary!.Status);
        Assert.Equal(2 * 750m + 350m, summary.Total);
        Assert.Equal(3, summary.ItemCount);
        Assert.Equal("Klavye, Mouse", summary.ProductNames);

        var stats = await EventuallyAsync(() => GetStatsAsync(customerId), s => s.OrderCount == 1);
        Assert.Equal(1850m, stats.TotalSpent);
    }

    [Fact]
    public async Task Kargolama_ve_iptal_okuma_modellerini_gunceller()
    {
        var customerId = Guid.NewGuid();
        var shipped = await PlaceOrderAsync(customerId, (Mouse, 1));
        var cancelled = await PlaceOrderAsync(customerId, (MonitorId, 1));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"/orders/{shipped}/ship", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"/orders/{cancelled}/cancel", null)).StatusCode);

        await EventuallyAsync(() => TryGetSummaryAsync(shipped), s => s?.Status == "Shipped");
        await EventuallyAsync(() => TryGetSummaryAsync(cancelled), s => s?.Status == "Cancelled");

        var list = await _client.GetFromJsonAsync<List<OrderSummary>>($"/customers/{customerId}/orders");
        Assert.Equal(2, list!.Count);

        var stats = await EventuallyAsync(() => GetStatsAsync(customerId), s => s.OrderCount == 1);
        Assert.Equal(350m, stats.TotalSpent); // iptal edilen sipariş istatistikten düşüldü
    }

    [Fact]
    public async Task Kargolanmis_siparis_iptal_edilemez_409()
    {
        var id = await PlaceOrderAsync(Guid.NewGuid(), (Keyboard, 1));
        await _client.PostAsync($"/orders/{id}/ship", null);

        var response = await _client.PostAsync($"/orders/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Gecersiz_komutlar_400_ve_404_doner()
    {
        var empty = await _client.PostAsJsonAsync("/orders", new { customerId = Guid.NewGuid(), items = Array.Empty<object>() });
        var unknown = await _client.PostAsJsonAsync("/orders", new { customerId = Guid.NewGuid(), items = new[] { new { productId = Guid.NewGuid(), quantity = 1 } } });
        var missing = await _client.PostAsync($"/orders/{Guid.NewGuid()}/ship", null);

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Urunler_listelenir()
    {
        var products = await _client.GetFromJsonAsync<List<object>>("/products");

        Assert.Equal(3, products!.Count);
    }

    private async Task<Guid> PlaceOrderAsync(Guid customerId, params (Guid ProductId, int Quantity)[] items)
    {
        var response = await _client.PostAsJsonAsync("/orders", new
        {
            customerId,
            items = items.Select(i => new { productId = i.ProductId, quantity = i.Quantity })
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<OrderSummary?> TryGetSummaryAsync(Guid id)
    {
        var response = await _client.GetAsync($"/orders/{id}");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<OrderSummary>() : null;
    }

    private async Task<CustomerStats> GetStatsAsync(Guid customerId) =>
        (await _client.GetFromJsonAsync<CustomerStats>($"/customers/{customerId}/stats"))!;

    private static async Task<T> EventuallyAsync<T>(Func<Task<T>> probe, Func<T, bool> until, int timeoutMs = 10_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            var value = await probe();
            if (until(value)) return value;
            if (DateTime.UtcNow > deadline) Assert.Fail($"Okuma modeli {timeoutMs}ms içinde güncellenmedi. Son değer: {value}");
            await Task.Delay(100);
        }
    }
}
