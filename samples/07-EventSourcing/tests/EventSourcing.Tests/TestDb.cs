using Microsoft.Data.Sqlite;

namespace EventSourcing.Tests;

public static class TestDb
{
    public static string NewFile() => $"es-test-{Guid.NewGuid():N}.db";

    public static void Delete(string file)
    {
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { file, file + "-wal", file + "-shm" })
            File.Delete(f);
    }
}
