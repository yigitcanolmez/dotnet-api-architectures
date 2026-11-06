using Microsoft.AspNetCore.Mvc;

namespace Legacy.Api.Controllers;

// [LEGACY] "God controller": siparişler, ürünler, müşteriler hepsi tek sınıfta.
// STAT_CD: "A" = aktif (verildi), "K" = kargoda, "I" = iptal.
// Hatalar ProblemDetails değil, legacy formatında: { "ERR_CD": "...", "ERR_MSG": "..." }.
[ApiController]
[Route("api")]
public class LegacyController : ControllerBase
{
    [HttpGet("orders")]
    public IActionResult GET_ORDERS()
    {
        var list = new List<Dictionary<string, object?>>();
        using var c = LegacyDb.OPEN();
        var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT ORD_NO, CUST_ID, STAT_CD, TUTAR, KAYIT_TRH FROM TBL_ORD_HDR ORDER BY KAYIT_TRH DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new()
            {
                ["ORD_NO"] = r.GetString(0),
                ["CUST_ID"] = r.GetString(1),
                ["STAT_CD"] = r.GetString(2),
                ["TUTAR"] = r.GetString(3),
                ["KAYIT_TRH"] = r.GetString(4)
            });
        }
        return Ok(list);
    }

    [HttpGet("orders/{id}")]
    public IActionResult GET_ORDER(string id)
    {
        using var c = LegacyDb.OPEN();
        var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT ORD_NO, CUST_ID, STAT_CD, TUTAR, KAYIT_TRH FROM TBL_ORD_HDR WHERE ORD_NO = $no";
        cmd.Parameters.AddWithValue("$no", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return NotFound(new { ERR_CD = "E404", ERR_MSG = "SIPARIS BULUNAMADI" });

        var hdr = new Dictionary<string, object?>
        {
            ["ORD_NO"] = r.GetString(0),
            ["CUST_ID"] = r.GetString(1),
            ["STAT_CD"] = r.GetString(2),
            ["TUTAR"] = r.GetString(3),
            ["KAYIT_TRH"] = r.GetString(4)
        };

        var dtl = c.CreateCommand();
        dtl.CommandText = "SELECT LN_NO, PRD_ID, PRD_NM, ADET, BRM_FYT FROM TBL_ORD_DTL WHERE ORD_NO = $no ORDER BY LN_NO";
        dtl.Parameters.AddWithValue("$no", id);
        using var dr = dtl.ExecuteReader();
        var lines = new List<object>();
        while (dr.Read())
            lines.Add(new { LN_NO = dr.GetInt32(0), PRD_ID = dr.GetString(1), PRD_NM = dr.GetString(2), ADET = dr.GetInt32(3), BRM_FYT = dr.GetString(4) });
        hdr["KALEMLER"] = lines;
        return Ok(hdr);
    }

    public class ORD_REQ
    {
        public string? CUST_ID { get; set; }
        public List<ORD_LN>? KALEMLER { get; set; }
    }
    public class ORD_LN
    {
        public string? PRD_ID { get; set; }
        public int ADET { get; set; }
    }

    [HttpPost("orders")]
    public IActionResult POST_ORDER([FromBody] ORD_REQ req)
    {
        // İş kuralları doğrudan controller'da, SQL ile iç içe.
        if (req.KALEMLER == null || req.KALEMLER.Count == 0 || req.KALEMLER.Count > 10)
            return BadRequest(new { ERR_CD = "E001", ERR_MSG = "KALEM SAYISI 1-10 ARASI OLMALI" });

        using var c = LegacyDb.OPEN();
        using var tx = c.BeginTransaction();
        var ordNo = Guid.NewGuid().ToString();
        decimal tutar = 0;
        var lnNo = 0;
        foreach (var k in req.KALEMLER)
        {
            if (k.ADET <= 0) return BadRequest(new { ERR_CD = "E002", ERR_MSG = "ADET SIFIRDAN BUYUK OLMALI" });

            var p = c.CreateCommand();
            p.Transaction = tx;
            p.CommandText = "SELECT PRD_NM, PRC FROM TBL_PRD WHERE PRD_ID = $id AND STAT_CD = 'A'";
            p.Parameters.AddWithValue("$id", k.PRD_ID ?? "");
            using var pr = p.ExecuteReader();
            if (!pr.Read()) return BadRequest(new { ERR_CD = "E003", ERR_MSG = "URUN BULUNAMADI: " + k.PRD_ID });
            var nm = pr.GetString(0);
            var fyt = pr.GetString(1);
            pr.Close();

            tutar += LegacyDb.PARSE_TUTAR(fyt) * k.ADET;
            var ins = c.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO TBL_ORD_DTL VALUES ($no, $ln, $prd, $nm, $adet, $fyt)";
            ins.Parameters.AddWithValue("$no", ordNo);
            ins.Parameters.AddWithValue("$ln", ++lnNo);
            ins.Parameters.AddWithValue("$prd", k.PRD_ID);
            ins.Parameters.AddWithValue("$nm", nm);
            ins.Parameters.AddWithValue("$adet", k.ADET);
            ins.Parameters.AddWithValue("$fyt", fyt);
            ins.ExecuteNonQuery();
        }

        var h = c.CreateCommand();
        h.Transaction = tx;
        h.CommandText = "INSERT INTO TBL_ORD_HDR VALUES ($no, $cust, 'A', $tutar, $trh)";
        h.Parameters.AddWithValue("$no", ordNo);
        h.Parameters.AddWithValue("$cust", req.CUST_ID ?? "");
        h.Parameters.AddWithValue("$tutar", LegacyDb.FMT_TUTAR(tutar));
        h.Parameters.AddWithValue("$trh", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
        h.ExecuteNonQuery();
        tx.Commit();

        return StatusCode(201, new { ORD_NO = ordNo, SONUC = "OK" });
    }

    [HttpPost("orders/{id}/ship")]
    public IActionResult SHIP(string id) => UPD_STAT(id, "K");

    [HttpPost("orders/{id}/cancel")]
    public IActionResult CANCEL(string id) => UPD_STAT(id, "I");

    private IActionResult UPD_STAT(string id, string yeni)
    {
        using var c = LegacyDb.OPEN();
        var s = c.CreateCommand();
        s.CommandText = "SELECT STAT_CD FROM TBL_ORD_HDR WHERE ORD_NO = $no";
        s.Parameters.AddWithValue("$no", id);
        var mevcut = s.ExecuteScalar() as string;
        if (mevcut == null) return NotFound(new { ERR_CD = "E404", ERR_MSG = "SIPARIS BULUNAMADI" });
        if (yeni == "K" && mevcut != "A") return Conflict(new { ERR_CD = "E409", ERR_MSG = "SADECE AKTIF SIPARIS KARGOLANABILIR" });
        if (yeni == "I" && mevcut == "K") return Conflict(new { ERR_CD = "E409", ERR_MSG = "KARGODAKI SIPARIS IPTAL EDILEMEZ" });
        if (yeni == "I" && mevcut == "I") return Conflict(new { ERR_CD = "E409", ERR_MSG = "SIPARIS ZATEN IPTAL" });

        var u = c.CreateCommand();
        u.CommandText = "UPDATE TBL_ORD_HDR SET STAT_CD = $s WHERE ORD_NO = $no";
        u.Parameters.AddWithValue("$s", yeni);
        u.Parameters.AddWithValue("$no", id);
        u.ExecuteNonQuery();
        return NoContent();
    }

    [HttpGet("products")]
    public IActionResult GET_PRODUCTS()
    {
        using var c = LegacyDb.OPEN();
        var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT PRD_ID, PRD_NM, PRC, STK_QTY, STAT_CD FROM TBL_PRD ORDER BY PRD_ID";
        using var r = cmd.ExecuteReader();
        var list = new List<object>();
        while (r.Read())
            list.Add(new { PRD_ID = r.GetString(0), PRD_NM = r.GetString(1), PRC = r.GetString(2), STK_QTY = r.GetInt32(3), STAT_CD = r.GetString(4) });
        return Ok(list);
    }

    // Henüz taşınmamış özellik: proxy'nin catch-all route'u ile her zaman legacy'den servis edilir.
    [HttpGet("customers")]
    public IActionResult GET_CUSTOMERS()
    {
        using var c = LegacyDb.OPEN();
        var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT CUST_ID, CUST_NM, CITY_CD FROM TBL_CUST";
        using var r = cmd.ExecuteReader();
        var list = new List<object>();
        while (r.Read())
            list.Add(new { CUST_ID = r.GetString(0), CUST_NM = r.GetString(1), CITY_CD = r.GetString(2) });
        return Ok(list);
    }
}
