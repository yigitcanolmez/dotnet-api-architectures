namespace ModularMonolith.Shared.Infrastructure.Inbox;

// Inbox tablosunun satırı: "Bu handler bu event'i işledi". (EventId, HandlerName) birlikte anahtardır,
// çünkü aynı event'i farklı modüllerin farklı handler'ları ayrı ayrı işler.
public sealed class InboxMessage
{
    private InboxMessage() { }

    public InboxMessage(Guid eventId, string handlerName)
    {
        EventId = eventId;
        HandlerName = handlerName;
        ProcessedOnUtc = DateTime.UtcNow;
    }

    public Guid EventId { get; private set; }
    public string HandlerName { get; private set; } = "";
    public DateTime ProcessedOnUtc { get; private set; }
}
