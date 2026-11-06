using System.Text.Json;
using ModularMonolith.Shared.Abstractions.Events;

namespace ModularMonolith.Shared.Infrastructure.Outbox;

// Outbox tablosunun satırı: "yayınlanmayı bekleyen integration event".
public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = "";
    public string Content { get; private set; } = "";
    public DateTime OccurredOnUtc { get; private set; }
    public DateTime? ProcessedOnUtc { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }

    public static OutboxMessage From(IIntegrationEvent integrationEvent)
    {
        var type = integrationEvent.GetType();
        return new OutboxMessage
        {
            Id = integrationEvent.Id,
            Type = $"{type.FullName}, {type.Assembly.GetName().Name}",
            Content = JsonSerializer.Serialize(integrationEvent, type),
            OccurredOnUtc = integrationEvent.OccurredOnUtc
        };
    }

    public IIntegrationEvent ToIntegrationEvent()
    {
        var type = System.Type.GetType(Type, throwOnError: true)!;
        return (IIntegrationEvent)JsonSerializer.Deserialize(Content, type)!;
    }
}
