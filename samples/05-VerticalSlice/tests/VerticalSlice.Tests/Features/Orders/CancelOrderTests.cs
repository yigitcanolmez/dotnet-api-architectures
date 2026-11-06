using System.Net;
using System.Net.Http.Json;
using static VerticalSlice.Tests.TestData;

namespace VerticalSlice.Tests.Features.Orders;

public sealed class CancelOrderTests(VerticalSliceApiFactory factory) : IClassFixture<VerticalSliceApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Placed_siparis_iptal_edilir_ve_stok_iade_edilir()
    {
        var before = await _client.GetStockAsync(MonitorId);
        var id = await _client.PlaceOrderAsync((MonitorId, 2));

        var response = await _client.PostAsync($"/orders/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Cancelled", (await _client.GetFromJsonAsync<OrderResponse>($"/orders/{id}"))!.Status);
        Assert.Equal(before, await _client.GetStockAsync(MonitorId));
    }

    [Fact]
    public async Task Kargolanmis_siparis_iptal_edilemez_409()
    {
        var id = await _client.PlaceOrderAsync((MouseId, 1));
        await _client.PostAsync($"/orders/{id}/ship", null);

        var response = await _client.PostAsync($"/orders/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Iptal_edilmis_siparis_tekrar_iptal_edilemez_409()
    {
        var id = await _client.PlaceOrderAsync((MouseId, 1));
        await _client.PostAsync($"/orders/{id}/cancel", null);

        var response = await _client.PostAsync($"/orders/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
