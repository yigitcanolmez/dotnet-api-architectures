namespace Onion.Application.Exceptions;

// [Application Services halkası] "Kaynak bulunamadı" bir use case kavramı → Api'de 404.
public class NotFoundException(string message) : Exception(message);
