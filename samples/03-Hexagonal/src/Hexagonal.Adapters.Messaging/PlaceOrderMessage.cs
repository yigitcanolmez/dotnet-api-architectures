namespace Hexagonal.Adapters.Messaging;

// Kuyruktaki mesajın şekli (ör. RabbitMQ'daki JSON). Core'un PlaceOrderCommand'ından bağımsız.
public record PlaceOrderMessage(Guid MessageId, Guid CustomerId, IReadOnlyList<PlaceOrderMessageItem> Items);

public record PlaceOrderMessageItem(Guid ProductId, int Quantity);
