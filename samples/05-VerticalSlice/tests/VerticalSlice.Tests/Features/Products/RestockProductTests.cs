using System.Net;
using System.Net.Http.Json;
using static VerticalSlice.Tests.TestData;

namespace VerticalSlice.Tests.Features.Products;

public sealed class RestockProductTests(VerticalSliceApiFactory factory) : IClassFixture<VerticalSliceApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Urunler_seed_ile_gelir()
    {
        var products = await _client.GetFromJsonAsync<List<ProductResponse>>("/products");

        Assert.Equal(3, products!.Count);
        Assert.Contains(products, p => p is { Name: "Klavye", Price: 750m });
    }

    [Fact]
    public async Task Stok_eklenir()
    {
        var before = await _client.GetStockAsync(KeyboardId);

        var response = await _client.PostAsJsonAsync($"/products/{KeyboardId}/restock", new { quantity = 5 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before + 5, await _client.GetStockAsync(KeyboardId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Gecersiz_miktar_400_doner(int quantity)
    {
        var response = await _client.PostAsJsonAsync($"/products/{KeyboardId}/restock", new { quantity });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Olmayan_urun_404_doner()
    {
        var response = await _client.PostAsJsonAsync($"/products/{Guid.NewGuid()}/restock", new { quantity = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
