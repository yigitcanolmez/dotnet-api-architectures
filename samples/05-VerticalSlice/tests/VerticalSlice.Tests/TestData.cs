using System.Net.Http.Json;

namespace VerticalSlice.Tests;

public static class TestData
{
    public static readonly Guid KeyboardId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid MouseId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid MonitorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public sealed record IdResponse(Guid Id);
    public sealed record OrderResponse(Guid Id, Guid CustomerId, string Status, decimal Total);
    public sealed record ProductResponse(Guid Id, string Name, decimal Price, string Currency, int Stock);

    public static object OrderBody(params (Guid ProductId, int Quantity)[] items) => new
    {
        customerId = Guid.NewGuid(),
        items = items.Select(i => new { productId = i.ProductId, quantity = i.Quantity })
    };

    public static async Task<Guid> PlaceOrderAsync(this HttpClient client, params (Guid, int)[] items)
    {
        var response = await client.PostAsJsonAsync("/orders", OrderBody(items));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    public static async Task<int> GetStockAsync(this HttpClient client, Guid productId)
    {
        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/products");
        return products!.Single(p => p.Id == productId).Stock;
    }
}
