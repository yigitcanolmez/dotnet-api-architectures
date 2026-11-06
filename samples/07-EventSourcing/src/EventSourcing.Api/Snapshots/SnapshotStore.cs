using System.Text.Json;
using EventSourcing.Api.Domain;
using Microsoft.Data.Sqlite;

namespace EventSourcing.Api.Snapshots;

// [Snapshots] Performans optimizasyonu, KAYNAK DEĞİL: snapshot tablosunu silseniz de hiçbir veri
// kaybolmaz (event'lerden yeniden üretilir). Uzun stream'lerde (binlerce event) yüklemeyi hızlandırır.
public sealed class SnapshotStore(string connectionString)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void EnsureSchema()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Snapshots (
                StreamId  TEXT    NOT NULL PRIMARY KEY,
                Version   INTEGER NOT NULL,
                Data      TEXT    NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public async Task<BankAccountSnapshot?> GetAsync(string streamId, CancellationToken ct)
    {
        await using var connection = Open();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Data FROM Snapshots WHERE StreamId = $stream";
        command.Parameters.AddWithValue("$stream", streamId);
        return await command.ExecuteScalarAsync(ct) is string data
            ? JsonSerializer.Deserialize<BankAccountSnapshot>(data, Json)
            : null;
    }

    public async Task SaveAsync(string streamId, BankAccountSnapshot snapshot, CancellationToken ct)
    {
        await using var connection = Open();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Snapshots (StreamId, Version, Data) VALUES ($stream, $version, $data)
            ON CONFLICT (StreamId) DO UPDATE SET Version = excluded.Version, Data = excluded.Data
            WHERE excluded.Version > Snapshots.Version
            """;
        command.Parameters.AddWithValue("$stream", streamId);
        command.Parameters.AddWithValue("$version", snapshot.Version);
        command.Parameters.AddWithValue("$data", JsonSerializer.Serialize(snapshot, Json));
        await command.ExecuteNonQueryAsync(ct);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
