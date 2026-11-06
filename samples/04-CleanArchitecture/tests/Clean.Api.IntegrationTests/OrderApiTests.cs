using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace Clean.Api.IntegrationTests;

// Uçtan uca: HTTP → Endpoint → MediatR pipeline → Handler → Domain → EF Core/SQLite → Interceptor → EventHandler.
// xUnit her test için sınıfın yeni bir örneğini oluşturur → her test yeni factory + yeni DB dosyası.
public sealed class OrderApiTests : IAsyncLifetime
{
    private const string Keyboard = "11111111-1111-1111-1111-111111111111";
    private const string Mouse = "22222222-2222-2222-2222-222222222222";
    private const string Monitor = "33333333-3333-3333-3333-333333333333";
    private const string Customer = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    private readonly CleanApiFactory _factory = new();
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private sealed record IdResponse(Guid Id);
    private sealed record ProductResponse(Guid Id, string Name, decimal Price, string Currency, int Stock);
    private sealed record OrderResponse(Guid Id, string Status, decimal Total, string? TrackingNumber);

    private async Task<HttpResponseMessage> PlaceOrderAsync(string productId, int quantity) =>
        await _client.PostAsJsonAsync("/orders", new
        {
            customerId = Customer,
            items = new[] { new { productId, quantity } }
        });

    private async Task<Guid> PlaceOrderIdAsync(string productId, int quantity)
    {
        var response = await PlaceOrderAsync(productId, quantity);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<int> StockOfAsync(string productId)
    {
        var products = await _client.GetFromJsonAsync<List<ProductResponse>>("/products");
        return products!.Single(p => p.Id == Guid.Parse(productId)).Stock;
    }

    [Fact]
    public async Task GetProducts_ReturnsSeededProducts()
    {
        var products = await _client.GetFromJsonAsync<List<ProductResponse>>("/products");

        Assert.Equal(3, products!.Count);
        Assert.Contains(products, p => p.Name == "Klavye" && p.Price == 750m && p.Stock == 10);
    }

    [Fact]
    public async Task Place_Ship_Cancel_Returns409()
    {
        var placeResponse = await PlaceOrderAsync(Keyboard, 2);
        Assert.Equal(HttpStatusCode.Created, placeResponse.StatusCode);
        var id = (await placeResponse.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        Assert.Equal($"/orders/{id}", placeResponse.Headers.Location?.OriginalString);

        var order = await _client.GetFromJsonAsync<OrderResponse>($"/orders/{id}");
        Assert.Equal("Placed", order!.Status);
        Assert.Equal(1500m, order.Total);

        var ship = await _client.PostAsJsonAsync($"/orders/{id}/ship", new { trackingNumber = "TRK-42" });
        Assert.Equal(HttpStatusCode.NoContent, ship.StatusCode);

        var cancel = await _client.PostAsJsonAsync($"/orders/{id}/cancel", new { reason = "vazgeçtim" });
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        var problem = await cancel.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(409, problem!.Status);
        Assert.Equal("Order.ShippedCannotBeCancelled", problem.Extensions["code"]?.ToString());

        order = await _client.GetFromJsonAsync<OrderResponse>($"/orders/{id}");
        Assert.Equal("Shipped", order!.Status);
        Assert.Equal("TRK-42", order.TrackingNumber);
    }

    [Fact]
    public async Task Place_DecreasesStock_Cancel_RestoresStock()
    {
        var id = await PlaceOrderIdAsync(Mouse, 4);
        Assert.Equal(16, await StockOfAsync(Mouse));

        var cancel = await _client.PostAsync($"/orders/{id}/cancel", null); // gövdesiz → varsayılan neden
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(20, await StockOfAsync(Mouse));

        var again = await _client.PostAsync($"/orders/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Place_InsufficientStock_Returns409_AndNothingIsSaved()
    {
        var response = await PlaceOrderAsync(Monitor, 6);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(5, await StockOfAsync(Monitor));
        Assert.Empty((await _client.GetFromJsonAsync<List<OrderResponse>>("/orders"))!);
    }

    [Fact]
    public async Task Place_InvalidBody_Returns400ValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/orders", new { customerId = Guid.Empty, items = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("CustomerId", problem!.Errors.Keys);
        Assert.Contains("Items", problem.Errors.Keys);
    }

    [Fact]
    public async Task Place_UnknownProduct_Returns404()
    {
        var response = await PlaceOrderAsync(Guid.NewGuid().ToString(), 1);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_Unknown_Returns404()
    {
        var response = await _client.GetAsync($"/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListOrders_ReturnsPlacedOrders()
    {
        await PlaceOrderIdAsync(Keyboard, 1);
        await PlaceOrderIdAsync(Mouse, 1);

        var orders = await _client.GetFromJsonAsync<List<OrderResponse>>("/orders");
        Assert.Equal(2, orders!.Count);
    }
}
