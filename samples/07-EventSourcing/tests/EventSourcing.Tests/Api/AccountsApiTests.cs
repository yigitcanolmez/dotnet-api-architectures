using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EventSourcing.Tests.Api;

public sealed class EventSourcingApiFactory : WebApplicationFactory<Program>
{
    private readonly string _file = TestDb.NewFile();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_file}");
        builder.UseSetting("Projection:PollIntervalMs", "100");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TestDb.Delete(_file);
    }
}

public sealed class AccountsApiTests(EventSourcingApiFactory factory) : IClassFixture<EventSourcingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record IdResponse(Guid Id);
    private sealed record AccountResponse(Guid Id, string Owner, decimal Balance, bool IsClosed, int Version, int? LoadedFromSnapshotVersion);
    private sealed record EventResponse(long GlobalPosition, int Version, string Type, DateTime Timestamp);
    private sealed record BalanceAt(decimal Balance, int Version);
    private sealed record Row(Guid AccountId, string Owner, decimal Balance, string Status);
    private sealed record Rebuild(int ReplayedEvents);

    [Fact]
    public async Task Temporal_query_gecmisteki_bakiyeyi_dondurur()
    {
        var id = await OpenAsync("Ayşe");
        await PostAmountAsync(id, "deposit", 200);
        await PostAmountAsync(id, "withdraw", 100);
        await PostAmountAsync(id, "deposit", 50);

        var events = (await _client.GetFromJsonAsync<List<EventResponse>>($"/accounts/{id}/events"))!;
        Assert.Equal(["AccountOpened", "MoneyDeposited", "MoneyWithdrawn", "MoneyDeposited"], events.Select(e => e.Type));

        var afterFirstDeposit = events[1].Timestamp.ToString("O");
        var past = await _client.GetFromJsonAsync<BalanceAt>($"/accounts/{id}/balance-at?timestamp={Uri.EscapeDataString(afterFirstDeposit)}");
        Assert.Equal(200, past!.Balance);
        Assert.Equal(2, past.Version);

        var now = await _client.GetFromJsonAsync<AccountResponse>($"/accounts/{id}");
        Assert.Equal(150, now!.Balance);
    }

    [Fact]
    public async Task Her_5_eventte_snapshot_alinir()
    {
        var id = await OpenAsync("Can");
        for (var i = 0; i < 5; i++) await PostAmountAsync(id, "deposit", 10); // toplam 6 event

        var account = await _client.GetFromJsonAsync<AccountResponse>($"/accounts/{id}");

        Assert.Equal(6, account!.Version);
        Assert.Equal(5, account.LoadedFromSnapshotVersion);
        Assert.Equal(50, account.Balance);
    }

    [Fact]
    public async Task Kurallar_ve_concurrency_409_doner()
    {
        var id = await OpenAsync("Deniz");
        await PostAmountAsync(id, "deposit", 100);

        var insufficient = await _client.PostAsJsonAsync($"/accounts/{id}/withdraw", new { amount = 500 });
        var stale = await _client.PostAsJsonAsync($"/accounts/{id}/deposit", new { amount = 5, expectedVersion = 1 });
        var invalid = await _client.PostAsJsonAsync($"/accounts/{id}/deposit", new { amount = 0 });
        await _client.PostAsync($"/accounts/{id}/close", null);
        var closed = await _client.PostAsJsonAsync($"/accounts/{id}/deposit", new { amount = 5 });

        Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/accounts/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Projeksiyon_guncellenir_ve_bastan_yeniden_olusturulabilir()
    {
        var id = await OpenAsync("Ece");
        await PostAmountAsync(id, "deposit", 300);
        await PostAmountAsync(id, "withdraw", 120.5m);

        var row = await EventuallyAsync(async () =>
            (await _client.GetFromJsonAsync<List<Row>>("/accounts"))!.SingleOrDefault(r => r.AccountId == id && r.Balance == 179.5m));
        Assert.Equal("Open", row.Status);

        var rebuild = await (await _client.PostAsync("/admin/projections/rebuild", null)).Content.ReadFromJsonAsync<Rebuild>();
        Assert.True(rebuild!.ReplayedEvents >= 3);

        var afterRebuild = (await _client.GetFromJsonAsync<List<Row>>("/accounts"))!.Single(r => r.AccountId == id);
        Assert.Equal(179.5m, afterRebuild.Balance);
    }

    private async Task<Guid> OpenAsync(string owner)
    {
        var response = await _client.PostAsJsonAsync("/accounts", new { owner });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task PostAmountAsync(Guid id, string action, decimal amount) =>
        (await _client.PostAsJsonAsync($"/accounts/{id}/{action}", new { amount })).EnsureSuccessStatusCode();

    private static async Task<T> EventuallyAsync<T>(Func<Task<T?>> probe) where T : class
    {
        for (var i = 0; i < 100; i++)
        {
            if (await probe() is { } value) return value;
            await Task.Delay(100);
        }
        throw new TimeoutException("Projeksiyon 10 sn içinde güncellenmedi.");
    }
}
