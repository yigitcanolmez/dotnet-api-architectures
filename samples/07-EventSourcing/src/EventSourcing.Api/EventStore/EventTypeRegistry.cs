using System.Text.Json;
using EventSourcing.Api.Domain;

namespace EventSourcing.Api.EventStore;

// [Event Store] Saklanan tip ADI ↔ .NET Type eşlemesi. Tip adı bir SÖZLEŞMEDİR:
// C# sınıfını yeniden adlandırsanız bile DB'deki "MoneyDeposited" kayıtları okunabilmeli.
// (AssemblyQualifiedName saklamak bu yüzden kötü bir fikirdir.)
public sealed class EventTypeRegistry
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, Type> _byName = new();
    private readonly Dictionary<Type, string> _byType = new();

    public EventTypeRegistry Register<TEvent>(string name) where TEvent : IEvent
    {
        _byName.Add(name, typeof(TEvent));
        _byType.Add(typeof(TEvent), name);
        return this;
    }

    public string NameOf(IEvent e) =>
        _byType.TryGetValue(e.GetType(), out var name)
            ? name
            : throw new InvalidOperationException($"Kayıtsız event tipi: {e.GetType().Name}");

    public string Serialize(IEvent e) => JsonSerializer.Serialize(e, e.GetType(), Json);

    public IEvent Deserialize(string name, string data) =>
        _byName.TryGetValue(name, out var type)
            ? (IEvent)JsonSerializer.Deserialize(data, type, Json)!
            : throw new InvalidOperationException($"Bilinmeyen event tipi: {name}");

    public static EventTypeRegistry CreateDefault() => new EventTypeRegistry()
        .Register<AccountOpened>("AccountOpened")
        .Register<MoneyDeposited>("MoneyDeposited")
        .Register<MoneyWithdrawn>("MoneyWithdrawn")
        .Register<AccountClosed>("AccountClosed");
}
