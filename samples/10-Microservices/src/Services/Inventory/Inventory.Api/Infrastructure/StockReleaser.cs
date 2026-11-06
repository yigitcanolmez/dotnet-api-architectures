// [Inventory — Uygulama servisi] Stok iadesi iki farklı mesajla tetiklenir:
//   ReleaseStock (saga'nın compensation KOMUTU) ve OrderCancelled (Ordering'in EVENT'i).
using Inventory.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Infrastructure;

public class StockReleaser(InventoryDbContext db, ILogger<StockReleaser> logger)
{
    public async Task ReleaseAsync(Guid orderId, string reason, CancellationToken ct)
    {
        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.OrderId == orderId, ct);
        if (reservation is null)
        {
            logger.LogWarning("Serbest bırakılacak rezervasyon yok {OrderId}", orderId);
            return;
        }

        var ids = reservation.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        if (reservation.Release(reason, products))
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Stok serbest bırakıldı (compensation) {OrderId} Sebep={Reason}", orderId, reason);
        }
        else
        {
            logger.LogInformation("Rezervasyon zaten {Status}, işlem yok {OrderId}", reservation.Status, orderId);
        }
    }
}
