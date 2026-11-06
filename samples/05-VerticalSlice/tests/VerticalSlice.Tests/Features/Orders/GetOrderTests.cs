using System.Net;
using System.Net.Http.Json;
using static VerticalSlice.Tests.TestData;

namespace VerticalSlice.Tests.Features.Orders;

public sealed class GetOrderTests(VerticalSliceApiFactory factory) : IClassFixture<VerticalSliceApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Olmayan_siparis_404_doner()
    {
        var response = await _client.GetAsync($"/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Liste_musteriye_gore_filtrelenir()
    {
        var customerId = Guid.NewGuid();
        await _client.PostAsJsonAsync("/orders", new { customerId, items = new[] { new { productId = MouseId, quantity = 1 } } });
        await _client.PlaceOrderAsync((MouseId, 1)); // başka müşteri

        var orders = await _client.GetFromJsonAsync<List<OrderResponse>>($"/orders?customerId={customerId}");

        Assert.Single(orders!);
        Assert.Equal(customerId, orders![0].CustomerId);
    }
}
