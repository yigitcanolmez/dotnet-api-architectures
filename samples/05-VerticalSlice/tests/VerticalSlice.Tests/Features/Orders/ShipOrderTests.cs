using System.Net;
using System.Net.Http.Json;
using static VerticalSlice.Tests.TestData;

namespace VerticalSlice.Tests.Features.Orders;

public sealed class ShipOrderTests(VerticalSliceApiFactory factory) : IClassFixture<VerticalSliceApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Placed_siparis_kargolanir()
    {
        var id = await _client.PlaceOrderAsync((KeyboardId, 1));

        var response = await _client.PostAsync($"/orders/{id}/ship", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Shipped", (await _client.GetFromJsonAsync<OrderResponse>($"/orders/{id}"))!.Status);
    }

    [Fact]
    public async Task Kargolanmis_siparis_tekrar_kargolanamaz()
    {
        var id = await _client.PlaceOrderAsync((KeyboardId, 1));
        await _client.PostAsync($"/orders/{id}/ship", null);

        var response = await _client.PostAsync($"/orders/{id}/ship", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Olmayan_siparis_404_doner()
    {
        var response = await _client.PostAsync($"/orders/{Guid.NewGuid()}/ship", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
