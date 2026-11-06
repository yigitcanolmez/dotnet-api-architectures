namespace Onion.Domain.Exceptions;

// [Domain Model] İş kuralı ihlali. Dış halkada (Api) HTTP 409'a çevrilir; Domain HTTP'yi bilmez.
public class DomainException(string message) : Exception(message);
