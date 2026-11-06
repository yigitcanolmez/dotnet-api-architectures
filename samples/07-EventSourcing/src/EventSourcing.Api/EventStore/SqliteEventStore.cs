using System.Globalization;
using EventSourcing.Api.Domain;
using Microsoft.Data.Sqlite;

namespace EventSourcing.Api.EventStore;

// [Event Store] SQLite üzerinde minimal event store. Gerçek projelerde: KurrentDB (EventStoreDB),
// Marten (PostgreSQL) gibi hazır çözümler. Burada amaç mekanizmayı ÇIPLAK görmek.
public sealed class SqliteEventStore(string connectionString, EventTypeRegistry registry, TimeProvider clock) : IEventStore
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    public void EnsureSchema()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        // UNIQUE(StreamId, Version) → aynı versiyona iki yazma fiziksel olarak imkânsız.
        // WAL: okuyucular (projeksiyon, sorgular) yazıcıları bloklamaz.
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS Events (
                GlobalPosition INTEGER PRIMARY KEY AUTOINCREMENT,
                StreamId       TEXT    NOT NULL,
                Version        INTEGER NOT NULL,
                Type           TEXT    NOT NULL,
                Data           TEXT    NOT NULL,
                Timestamp      TEXT    NOT NULL,
                UNIQUE (StreamId, Version)
            );
            """;
        command.ExecuteNonQuery();
    }

    public async Task AppendAsync(string streamId, int expectedVersion, IReadOnlyList<IEvent> events, CancellationToken ct = default)
    {
        if (events.Count == 0) return;

        await using var connection = Open();
        // BEGIN IMMEDIATE: yazma kilidi baştan alınır → eşzamanlı yazanlar sıraya girer.
        await using var tx = connection.BeginTransaction(deferred: false);

        var actual = await CurrentVersionAsync(connection, tx, streamId, ct);
        if (actual != expectedVersion)
            throw new ConcurrencyException(streamId, expectedVersion, actual);

        var now = clock.GetUtcNow().UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var version = expectedVersion;
        foreach (var e in events)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO Events (StreamId, Version, Type, Data, Timestamp)
                VALUES ($stream, $version, $type, $data, $ts)
                """;
            insert.Parameters.AddWithValue("$stream", streamId);
            insert.Parameters.AddWithValue("$version", ++version);
            insert.Parameters.AddWithValue("$type", registry.NameOf(e));
            insert.Parameters.AddWithValue("$data", registry.Serialize(e));
            insert.Parameters.AddWithValue("$ts", now);
            try
            {
                await insert.ExecuteNonQueryAsync(ct);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT (UNIQUE)
            {
                throw new ConcurrencyException(streamId, expectedVersion, version);
            }
        }

        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<StoredEvent>> ReadStreamAsync(
        string streamId, DateTime? upToTimestamp = null, int afterVersion = 0, CancellationToken ct = default)
    {
        await using var connection = Open();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT GlobalPosition, StreamId, Version, Type, Data, Timestamp FROM Events
            WHERE StreamId = $stream AND Version > $after AND ($upTo IS NULL OR Timestamp <= $upTo)
            ORDER BY Version
            """;
        command.Parameters.AddWithValue("$stream", streamId);
        command.Parameters.AddWithValue("$after", afterVersion);
        command.Parameters.AddWithValue("$upTo", upToTimestamp is { } t
            ? t.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture)
            : DBNull.Value);
        return await ReadEventsAsync(command, ct);
    }

    public async Task<IReadOnlyList<StoredEvent>> ReadAllAsync(long fromPosition, int maxCount = 100, CancellationToken ct = default)
    {
        await using var connection = Open();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT GlobalPosition, StreamId, Version, Type, Data, Timestamp FROM Events
            WHERE GlobalPosition > $from ORDER BY GlobalPosition LIMIT $max
            """;
        command.Parameters.AddWithValue("$from", fromPosition);
        command.Parameters.AddWithValue("$max", maxCount);
        return await ReadEventsAsync(command, ct);
    }

    private async Task<IReadOnlyList<StoredEvent>> ReadEventsAsync(SqliteCommand command, CancellationToken ct)
    {
        var result = new List<StoredEvent>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var type = reader.GetString(3);
            var data = reader.GetString(4);
            result.Add(new StoredEvent(
                reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2), type, data,
                DateTime.ParseExact(reader.GetString(5), TimestampFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
                registry.Deserialize(type, data)));
        }
        return result;
    }

    private static async Task<int> CurrentVersionAsync(SqliteConnection connection, SqliteTransaction tx, string streamId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM Events WHERE StreamId = $stream";
        command.Parameters.AddWithValue("$stream", streamId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
