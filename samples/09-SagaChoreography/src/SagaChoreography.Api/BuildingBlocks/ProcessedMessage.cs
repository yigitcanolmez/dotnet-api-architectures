using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace SagaChoreography.BuildingBlocks;

// [Inbox / Idempotent Consumer] In-memory transport'ta bile retry ve at-least-once teslim nedeniyle
// aynı mesaj birden fazla işlenebilir. Her servis, işlediği (MessageId, Consumer) çiftini KENDİ veritabanında,
// iş verisiyle AYNI SaveChanges içinde saklar. Gerçek sistemde bu küçük yardımcı paylaşılan bir kütüphane olurdu.
public sealed class ProcessedMessage
{
    private ProcessedMessage() { }

    public ProcessedMessage(Guid messageId, string consumer)
    {
        MessageId = messageId;
        Consumer = consumer;
        ProcessedOnUtc = DateTime.UtcNow;
    }

    public Guid MessageId { get; private set; }
    public string Consumer { get; private set; } = "";
    public DateTime ProcessedOnUtc { get; private set; }
}

public interface IInboxDbContext
{
    DbSet<ProcessedMessage> ProcessedMessages { get; }
}

public static class InboxExtensions
{
    public static void ConfigureInbox(this ModelBuilder modelBuilder) =>
        modelBuilder.Entity<ProcessedMessage>(b =>
        {
            b.ToTable("ProcessedMessages");
            b.HasKey(x => new { x.MessageId, x.Consumer }); // unique: yarış durumunda ikinci insert patlar → retry → atlanır
        });

    public static Task<bool> IsAlreadyProcessedAsync<T>(this IInboxDbContext db, ConsumeContext<T> context, object consumer)
        where T : class
    {
        var messageId = context.MessageId ?? throw new InvalidOperationException("MessageId yok.");
        var name = consumer.GetType().Name;
        return db.ProcessedMessages.AnyAsync(x => x.MessageId == messageId && x.Consumer == name, context.CancellationToken);
    }

    public static void MarkAsProcessed<T>(this IInboxDbContext db, ConsumeContext<T> context, object consumer)
        where T : class =>
        db.ProcessedMessages.Add(new ProcessedMessage(context.MessageId!.Value, consumer.GetType().Name));
}
