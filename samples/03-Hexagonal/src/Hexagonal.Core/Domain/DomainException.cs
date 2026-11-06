namespace Hexagonal.Core.Domain;

// İş kuralı ihlali. Hangi HTTP koduna (409) çevrileceğine driving adapter (Web) karar verir.
public class DomainException(string message) : Exception(message);
