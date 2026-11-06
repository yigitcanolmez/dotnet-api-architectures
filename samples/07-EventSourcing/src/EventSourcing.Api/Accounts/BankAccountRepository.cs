using EventSourcing.Api.Domain;
using EventSourcing.Api.EventStore;
using EventSourcing.Api.Snapshots;

namespace EventSourcing.Api.Accounts;

// Aggregate'i yükleme (snapshot + sonraki event'ler) ve kaydetme (append + gerekirse snapshot).
// "Repository" burada bir tabloya değil, bir event STREAM'ine karşılık gelir.
public sealed class BankAccountRepository(IEventStore store, SnapshotStore snapshots, IConfiguration config)
{
    private int SnapshotEvery => config.GetValue("Snapshots:Every", 5);

    public static string StreamId(Guid accountId) => $"account-{accountId}";

    public async Task<(BankAccount Account, int? SnapshotVersion)?> LoadAsync(Guid id, CancellationToken ct)
    {
        var streamId = StreamId(id);
        var snapshot = await snapshots.GetAsync(streamId, ct);
        var events = await store.ReadStreamAsync(streamId, afterVersion: snapshot?.Version ?? 0, ct: ct);

        if (snapshot is null && events.Count == 0)
            return null;

        return (BankAccount.FromSnapshot(snapshot, events.Select(e => e.Event)), snapshot?.Version);
    }

    // expectedVersion verilirse (istemcinin gördüğü versiyon) onunla, verilmezse yüklenen versiyonla yazılır.
    public async Task SaveAsync(BankAccount account, int? expectedVersion, CancellationToken ct)
    {
        var before = expectedVersion ?? account.PersistedVersion;
        await store.AppendAsync(StreamId(account.Id), before, account.Uncommitted, ct);
        account.MarkCommitted();

        // Her N event'te bir snapshot: 5'in katı sınırı geçildiyse yeni durumu dondur.
        if (account.Version / SnapshotEvery > before / SnapshotEvery)
            await snapshots.SaveAsync(StreamId(account.Id), account.ToSnapshot(), ct);
    }
}
