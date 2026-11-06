using EventSourcing.Api.Domain;

namespace EventSourcing.Api.EventStore;

// [Event Store] Append-only, stream bazlı, sıralı event deposu. UPDATE/DELETE YOK.
public interface IEventStore
{
    // expectedVersion: stream'in yazmadan önceki son versiyonu (yeni stream için 0).
    // Uyuşmazsa ConcurrencyException (optimistic concurrency).
    Task AppendAsync(string streamId, int expectedVersion, IReadOnlyList<IEvent> events, CancellationToken ct = default);

    // Bir stream'in event'leri. upToTimestamp → temporal query ("o andaki durum");
    // afterVersion → snapshot'tan sonraki event'ler.
    Task<IReadOnlyList<StoredEvent>> ReadStreamAsync(
        string streamId, DateTime? upToTimestamp = null, int afterVersion = 0, CancellationToken ct = default);

    // TÜM stream'lerin event'leri, global sırayla (projeksiyonlar için catch-up subscription).
    Task<IReadOnlyList<StoredEvent>> ReadAllAsync(long fromPosition, int maxCount = 100, CancellationToken ct = default);
}

// Event + metadata. GlobalPosition tüm store'da artan sıra; Version stream içi sıra.
public sealed record StoredEvent(
    long GlobalPosition, string StreamId, int Version, string Type, string Data, DateTime Timestamp, IEvent Event);

public sealed class ConcurrencyException(string streamId, int expectedVersion, int actualVersion)
    : Exception($"'{streamId}' stream'i için beklenen versiyon {expectedVersion}, gerçek versiyon {actualVersion}. " +
                "Başka bir işlem araya girdi; güncel durumu okuyup tekrar deneyin.")
{
    public int ExpectedVersion { get; } = expectedVersion;
    public int ActualVersion { get; } = actualVersion;
}
