namespace Cqrs.Api.Write.Domain;

public sealed class DomainException(string message) : Exception(message);
