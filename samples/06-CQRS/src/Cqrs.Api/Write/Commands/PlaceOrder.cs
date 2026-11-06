using Cqrs.Api.Common;
using Cqrs.Api.Write.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cqrs.Api.Write.Commands;

// [Write / Command] İsim NİYET belirtir: "CreateOrder/UpdateOrder" değil "PlaceOrder".
public sealed record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<PlaceOrderItem> Items);
public sealed record PlaceOrderItem(Guid ProductId, int Quantity);

// Komut handler'ı sadece oluşturulan ID'yi döner — sipariş DTO'su döndürmek okuma tarafının işi.
public sealed class PlaceOrderHandler(WriteDbContext db) : ICommandHandler<PlaceOrderCommand, Guid>
{
    public async Task<Guid> HandleAsync(PlaceOrderCommand command, CancellationToken ct)
    {
        Validate(command);

        var ids = command.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var missing = ids.Where(id => !products.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw new CommandValidationException(new Dictionary<string, string[]>
            {
                ["Items"] = [$"Bilinmeyen ürün: {string.Join(", ", missing)}"]
            });

        var order = Order.Place(command.CustomerId,
            command.Items.Select(i => (products[i.ProductId], i.Quantity)).ToList());

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct); // → OrderPlaced event'i yayınlanır
        return order.Id;
    }

    private static void Validate(PlaceOrderCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        if (command.CustomerId == Guid.Empty)
            errors["CustomerId"] = ["CustomerId boş olamaz."];
        if (command.Items is null || command.Items.Count is 0 or > Order.MaxLines)
            errors["Items"] = [$"Sipariş 1 ile {Order.MaxLines} arasında kalem içermeli."];
        else if (command.Items.Any(i => i.Quantity <= 0))
            errors["Items.Quantity"] = ["Adet 0'dan büyük olmalı."];

        if (errors.Count > 0) throw new CommandValidationException(errors);
    }
}
