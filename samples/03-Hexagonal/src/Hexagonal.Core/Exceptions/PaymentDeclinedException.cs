namespace Hexagonal.Core.Exceptions;

// Ödeme driven port'u (IPaymentGateway) reddetti.
public class PaymentDeclinedException(string reason) : Exception($"Ödeme reddedildi: {reason}");
