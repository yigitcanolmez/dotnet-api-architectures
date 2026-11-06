using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Legacy.Api;

// Legacy "DAL": statik sınıf, global bağlantı cümlesi, ham SQL. (Kötü örnek — bilerek.)
public static class LegacyDb
{
    public static string CONN_STR = "";

    // Legacy sistem tutarları "1.500,00" biçiminde STRING olarak saklar/döner.
    public static readonly NumberFormatInfo TR_FMT = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = ".", NumberGroupSizes = [3] };

    public static string FMT_TUTAR(decimal d) => d.ToString("#,##0.00", TR_FMT);
    public static decimal PARSE_TUTAR(string s) => decimal.Parse(s, NumberStyles.Number, TR_FMT);

    public static SqliteConnection OPEN()
    {
        var c = new SqliteConnection(CONN_STR);
        c.Open();
        return c;
    }

    public static void INIT_DB()
    {
        using var c = OPEN();
        var cmd = c.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS TBL_PRD (PRD_ID TEXT PRIMARY KEY, PRD_NM TEXT, PRC TEXT, STK_QTY INTEGER, STAT_CD TEXT);
            CREATE TABLE IF NOT EXISTS TBL_CUST (CUST_ID TEXT PRIMARY KEY, CUST_NM TEXT, CITY_CD TEXT);
            CREATE TABLE IF NOT EXISTS TBL_ORD_HDR (ORD_NO TEXT PRIMARY KEY, CUST_ID TEXT, STAT_CD TEXT, TUTAR TEXT, KAYIT_TRH TEXT);
            CREATE TABLE IF NOT EXISTS TBL_ORD_DTL (ORD_NO TEXT, LN_NO INTEGER, PRD_ID TEXT, PRD_NM TEXT, ADET INTEGER, BRM_FYT TEXT);

            INSERT OR IGNORE INTO TBL_PRD VALUES ('11111111-1111-1111-1111-111111111111', 'KLAVYE',  '750,00',   10, 'A');
            INSERT OR IGNORE INTO TBL_PRD VALUES ('22222222-2222-2222-2222-222222222222', 'MOUSE',   '350,00',   20, 'A');
            INSERT OR IGNORE INTO TBL_PRD VALUES ('33333333-3333-3333-3333-333333333333', 'MONITOR', '6.500,00',  5, 'A');
            -- Satıştan kalkmış ama silinmemiş ürün (legacy'de "P" = pasif). ACL bunu domain'e "satılamaz" olarak çevirir.
            INSERT OR IGNORE INTO TBL_PRD VALUES ('44444444-4444-4444-4444-444444444444', 'FAKS CIHAZI', '1.200,00', 3, 'P');

            INSERT OR IGNORE INTO TBL_CUST VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'AHMET YILMAZ', '34');
            INSERT OR IGNORE INTO TBL_CUST VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'AYSE KAYA',    '06');
            """;
        cmd.ExecuteNonQuery();
    }
}
