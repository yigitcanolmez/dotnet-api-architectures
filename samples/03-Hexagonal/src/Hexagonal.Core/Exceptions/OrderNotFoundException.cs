namespace Hexagonal.Core.Exceptions;

public class OrderNotFoundException(Guid orderId) : Exception($"Sipariş bulunamadı: {orderId}");
