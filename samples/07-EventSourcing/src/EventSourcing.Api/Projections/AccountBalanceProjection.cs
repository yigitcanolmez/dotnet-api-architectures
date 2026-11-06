using System.Globalization;
using EventSourcing.Api.Domain;
using EventSourcing.Api.EventStore;
using Microsoft.Data.Sqlite;

namespace EventSourcing.Api.Projections;

// [Projections] Event'lerden türetilmiş OKUMA MODELİ (AccountBalances tablosu).
// Bu tablo "tek doğruluk kaynağı" DEĞİLDİR; istediğiniz an silip event'lerden yeniden üretebilirsiniz.
// Checkpoint (son işlenen GlobalPosition) okuma modeliyle AYNI transaction'da güncellenir → tam bir kez etkisi.
public sealed class AccountBalanceProjection(string connectionString, IEventStore store, ILogger<AccountBalanceProjection> logger)
{
    private const string Name = "AccountBalances";
    private readonly SemaphoreSlim _gate = new(1, 1); // worker ile rebuild aynı anda çalışmasın

    public sealed record AccountBalanceRow(Guid AccountId, string Owner, decimal Balance, string Status, int Version, string LastUpdated);

    public void EnsureSchema()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS AccountBalances (
                AccountId   TEXT    NOT NULL PRIMARY KEY,
                Owner       TEXT    NOT NULL,
                Balance     TEXT    NOT NULL,   -- decimal, invariant culture metin olarak
                Status      TEXT    NOT NULL,
                Version     INTEGER NOT NULL,
                LastUpdated TEXT    NOT NULL
            );
            CREATE TABLE IF NOT EXISTS ProjectionCheckpoints (
                Name     TEXT    NOT NULL PRIMARY KEY,
                Position INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    // Catch-up subscription: checkpoint'ten sonraki event'leri parti parti işle.
    public async Task<int> CatchUpAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { return await ProcessNewEventsAsync(ct); }
        finally { _gate.Release(); }
    }

    // ES'nin süper gücü: okuma modelini sil, tüm geçmişi baştan oynat.
    public async Task<int> RebuildAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using (var connection = Open())
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM AccountBalances; DELETE FROM ProjectionCheckpoints WHERE Name = $name;";
                command.Parameters.AddWithValue("$name", Name);
                await command.ExecuteNonQueryAsync(ct);
            }
            var replayed = await ProcessNewEventsAsync(ct);
            logger.LogInformation("Projeksiyon yeniden oluşturuldu: {Count} event oynatıldı", replayed);
            return replayed;
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<AccountBalanceRow>> ListAsync(CancellationToken ct)
    {
        await using var connection = Open();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT AccountId, Owner, Balance, Status, Version, LastUpdated FROM AccountBalances ORDER BY Owner";
        var rows = new List<AccountBalanceRow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new AccountBalanceRow(reader.GetGuid(0), reader.GetString(1), reader.GetDecimal(2),
                reader.GetString(3), reader.GetInt32(4), reader.GetString(5)));
        return rows;
    }

    public async Task<long> GetCheckpointAsync(CancellationToken ct)
    {
        await using var connection = Open();
        return await GetCheckpointAsync(connection, null, ct);
    }

    private async Task<int> ProcessNewEventsAsync(CancellationToken ct)
    {
        var processed = 0;
        while (true)
        {
            var checkpoint = await GetCheckpointAsync(ct);
            var batch = await store.ReadAllAsync(checkpoint, 100, ct);
            if (batch.Count == 0) return processed;

            await using var connection = Open();
            await using var tx = connection.BeginTransaction();
            foreach (var stored in batch)
                await ApplyAsync(connection, tx, stored, ct);
            await SetCheckpointAsync(connection, tx, batch[^1].GlobalPosition, ct);
            await tx.CommitAsync(ct);
            processed += batch.Count;
        }
    }

    private static async Task ApplyAsync(SqliteConnection connection, SqliteTransaction tx, StoredEvent stored, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        var ts = stored.Timestamp.ToString("O", CultureInfo.InvariantCulture);

        switch (stored.Event)
        {
            case AccountOpened e:
                command.CommandText = """
                    INSERT INTO AccountBalances (AccountId, Owner, Balance, Status, Version, LastUpdated)
                    VALUES ($id, $owner, '0', 'Open', $version, $ts)
                    """;
                command.Parameters.AddWithValue("$id", e.AccountId.ToString());
                command.Parameters.AddWithValue("$owner", e.Owner);
                break;
            case MoneyDeposited e:
                await ChangeBalanceAsync(connection, tx, e.AccountId, +e.Amount, stored.Version, ts, ct);
                return;
            case MoneyWithdrawn e:
                await ChangeBalanceAsync(connection, tx, e.AccountId, -e.Amount, stored.Version, ts, ct);
                return;
            case AccountClosed e:
                command.CommandText = "UPDATE AccountBalances SET Status = 'Closed', Version = $version, LastUpdated = $ts WHERE AccountId = $id";
                command.Parameters.AddWithValue("$id", e.AccountId.ToString());
                break;
            default:
                return; // bu projeksiyonu ilgilendirmeyen event
        }

        command.Parameters.AddWithValue("$version", stored.Version);
        command.Parameters.AddWithValue("$ts", ts);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task ChangeBalanceAsync(SqliteConnection connection, SqliteTransaction tx,
        Guid accountId, decimal delta, int version, string ts, CancellationToken ct)
    {
        await using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT Balance FROM AccountBalances WHERE AccountId = $id";
        select.Parameters.AddWithValue("$id", accountId.ToString());
        var current = decimal.Parse((string)(await select.ExecuteScalarAsync(ct))!, CultureInfo.InvariantCulture);

        await using var update = connection.CreateCommand();
        update.Transaction = tx;
        update.CommandText = "UPDATE AccountBalances SET Balance = $balance, Version = $version, LastUpdated = $ts WHERE AccountId = $id";
        update.Parameters.AddWithValue("$id", accountId.ToString());
        update.Parameters.AddWithValue("$balance", (current + delta).ToString(CultureInfo.InvariantCulture));
        update.Parameters.AddWithValue("$version", version);
        update.Parameters.AddWithValue("$ts", ts);
        await update.ExecuteNonQueryAsync(ct);
    }

    private static async Task<long> GetCheckpointAsync(SqliteConnection connection, SqliteTransaction? tx, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT Position FROM ProjectionCheckpoints WHERE Name = $name";
        command.Parameters.AddWithValue("$name", Name);
        return await command.ExecuteScalarAsync(ct) is long position ? position : 0;
    }

    private static async Task SetCheckpointAsync(SqliteConnection connection, SqliteTransaction tx, long position, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            INSERT INTO ProjectionCheckpoints (Name, Position) VALUES ($name, $pos)
            ON CONFLICT (Name) DO UPDATE SET Position = excluded.Position
            """;
        command.Parameters.AddWithValue("$name", Name);
        command.Parameters.AddWithValue("$pos", position);
        await command.ExecuteNonQueryAsync(ct);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
