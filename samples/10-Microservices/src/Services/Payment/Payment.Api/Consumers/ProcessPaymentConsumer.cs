// [Payment — Saga katılımcısı / PIVOT adım] Komutu al, tahsilatı yap, sonucu event olarak yayınla.
// Ödemenin İKİ KEZ çekilmemesi kritik → EF Inbox + Payments.OrderId UNIQUE.
using Contracts.Commands;
using Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Payment.Api.Domain;
using Payment.Api.Infrastructure;

namespace Payment.Api.Consumers;

public class ProcessPaymentConsumer(PaymentDbContext db, ILogger<ProcessPaymentConsumer> logger) : IConsumer<ProcessPayment>
{
    public async Task Consume(ConsumeContext<ProcessPayment> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == msg.OrderId, ct);
        if (payment is not null)
        {
            logger.LogInformation("Tekrarlanan ProcessPayment {OrderId}; tekrar tahsilat YAPILMADI, önceki sonuç yayınlanıyor", msg.OrderId);
        }
        else
        {
            payment = Domain.Payment.Process(msg.OrderId, msg.CustomerId, msg.Amount);
            db.Payments.Add(payment);
        }

        if (payment.Status == PaymentStatus.Completed)
            await context.Publish(new PaymentCompleted(payment.OrderId, payment.Id, payment.Amount), ct);
        else
            await context.Publish(new PaymentFailed(payment.OrderId, payment.FailureReason ?? "Ödeme reddedildi"), ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Ödeme işlendi {OrderId} Tutar={Amount} Durum={Status}", payment.OrderId, payment.Amount, payment.Status);
    }
}
