using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using static VerticalSlice.Tests.TestData;

namespace VerticalSlice.Tests.Features.Orders;

// Slice bazlı integration test: dilimi uçtan uca (validator + handler + domain + EF) doğrular.
public sealed class PlaceOrderTests(VerticalSliceApiFactory factory) : IClassFixture<VerticalSliceApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Gecerli_siparis_201_doner_ve_stok_duser()
    {
        var before = await _client.GetStockAsync(MouseId);

        var response = await _client.PostAsJsonAsync("/orders", OrderBody((KeyboardId, 1), (MouseId, 2)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        Assert.Equal($"/orders/{id}", response.Headers.Location!.OriginalString);

        var order = await _client.GetFromJsonAsync<OrderResponse>($"/orders/{id}");
        Assert.Equal("Placed", order!.Status);
        Assert.Equal(750m + 2 * 350m, order.Total);
        Assert.Equal(before - 2, await _client.GetStockAsync(MouseId));
    }

    [Fact]
    public async Task Gecersiz_istek_validation_problem_doner()
    {
        var response = await _client.PostAsJsonAsync("/orders", new { customerId = Guid.Empty, items = new[] { new { productId = KeyboardId, quantity = 0 } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("CustomerId", problem!.Errors.Keys);
        Assert.Contains("Items[0].Quantity", problem.Errors.Keys);
    }

    [Fact]
    public async Task On_bir_kalem_reddedilir()
    {
        var items = Enumerable.Range(0, 11).Select(_ => (MouseId, 1)).ToArray();

        var response = await _client.PostAsJsonAsync("/orders", OrderBody(items));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bilinmeyen_urun_400_doner()
    {
        var response = await _client.PostAsJsonAsync("/orders", OrderBody((Guid.NewGuid(), 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Yetersiz_stok_409_doner()
    {
        var response = await _client.PostAsJsonAsync("/orders", OrderBody((MonitorId, 999)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
