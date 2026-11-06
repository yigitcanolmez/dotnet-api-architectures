using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Cqrs.Api.Read;

// [Read] Okuma tarafı EF Core ve domain kullanmaz: ham SQL + Dapper ile ekran-özel, denormalize tablolar.
// Seviye 2 CQRS: AYNI veritabanı dosyası, AYRI tablolar (OrderSummaries, CustomerStats).
public sealed class ReadDb(string connectionString)
{
    static ReadDb()
    {
        // EF Core SQLite Guid'leri büyük harfli TEXT olarak saklar; Dapper da aynı formatı kullansın.
        SqlMapper.RemoveTypeMap(typeof(Guid));
        SqlMapper.RemoveTypeMap(typeof(Guid?));
        SqlMapper.AddTypeHandler(new GuidAsTextHandler());
    }

    public IDbConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    public void EnsureSchema()
    {
        using var connection = Open();
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS OrderSummaries (
                OrderId      TEXT    NOT NULL PRIMARY KEY,
                CustomerId   TEXT    NOT NULL,
                Status       TEXT    NOT NULL,
                Total        NUMERIC NOT NULL,
                ItemCount    INTEGER NOT NULL,
                ProductNames TEXT    NOT NULL,   -- "Klavye, Mouse" (bilinçli denormalizasyon)
                LastUpdated  TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_OrderSummaries_CustomerId ON OrderSummaries (CustomerId);

            CREATE TABLE IF NOT EXISTS CustomerStats (
                CustomerId  TEXT    NOT NULL PRIMARY KEY,
                OrderCount  INTEGER NOT NULL,
                TotalSpent  NUMERIC NOT NULL
            );
            """);
    }

    private sealed class GuidAsTextHandler : SqlMapper.TypeHandler<Guid>
    {
        public override void SetValue(IDbDataParameter parameter, Guid value) =>
            parameter.Value = value.ToString().ToUpperInvariant();

        public override Guid Parse(object value) => Guid.Parse((string)value);
    }
}
