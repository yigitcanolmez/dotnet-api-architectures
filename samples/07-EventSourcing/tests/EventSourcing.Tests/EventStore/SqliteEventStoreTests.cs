using EventSourcing.Api.Domain;
using EventSourcing.Api.EventStore;

namespace EventSourcing.Tests.EventStore;

public sealed class SqliteEventStoreTests : IDisposable
{
    private readonly string _file = TestDb.NewFile();
    private readonly SqliteEventStore _store;

    public SqliteEventStoreTests()
    {
        _store = new SqliteEventStore($"Data Source={_file}", EventTypeRegistry.CreateDefault(), TimeProvider.System);
        _store.EnsureSchema();
    }

    public void Dispose() => TestDb.Delete(_file);

    [Fact]
    public async Task Append_ve_okuma_sirayi_ve_tipleri_korur()
    {
        var id = Guid.NewGuid();
        await _store.AppendAsync("account-1", 0, [new AccountOpened(id, "Ali"), new MoneyDeposited(id, 10.5m)]);

        var events = await _store.ReadStreamAsync("account-1");

        Assert.Equal([1, 2], events.Select(e => e.Version));
        Assert.Equal(new MoneyDeposited(id, 10.5m), events[1].Event);
        Assert.Equal("MoneyDeposited", events[1].Type);
    }

    [Fact]
    public async Task Eski_versiyonla_yazma_ConcurrencyException_firlatir()
    {
        var id = Guid.NewGuid();
        await _store.AppendAsync("account-2", 0, [new AccountOpened(id, "Ali")]);
        await _store.AppendAsync("account-2", 1, [new MoneyDeposited(id, 100)]); // A kullanıcısı

        // B kullanıcısı hâlâ versiyon 1'i gördüğünü sanıyor
        var ex = await Assert.ThrowsAsync<ConcurrencyException>(() =>
            _store.AppendAsync("account-2", 1, [new MoneyWithdrawn(id, 100)]));

        Assert.Equal(1, ex.ExpectedVersion);
        Assert.Equal(2, ex.ActualVersion);
        Assert.Equal(2, (await _store.ReadStreamAsync("account-2")).Count);
    }

    [Fact]
    public async Task Esz_zamanli_yazmalardan_sadece_biri_basarili_olur()
    {
        var id = Guid.NewGuid();
        await _store.AppendAsync("account-3", 0, [new AccountOpened(id, "Ali")]);

        var attempts = Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
        {
            try { await _store.AppendAsync("account-3", 1, [new MoneyDeposited(id, i + 1)]); return true; }
            catch (ConcurrencyException) { return false; }
        }));
        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(ok => ok));
        Assert.Equal(2, (await _store.ReadStreamAsync("account-3")).Count);
    }

    [Fact]
    public async Task ReadAll_global_pozisyondan_devam_eder()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await _store.AppendAsync("account-a", 0, [new AccountOpened(a, "A")]);
        await _store.AppendAsync("account-b", 0, [new AccountOpened(b, "B")]);
        await _store.AppendAsync("account-a", 1, [new MoneyDeposited(a, 1)]);

        var first = await _store.ReadAllAsync(0, maxCount: 2);
        var rest = await _store.ReadAllAsync(first[^1].GlobalPosition);

        Assert.Equal(["account-a", "account-b"], first.Select(e => e.StreamId));
        Assert.Equal("account-a", Assert.Single(rest).StreamId);
    }
}
