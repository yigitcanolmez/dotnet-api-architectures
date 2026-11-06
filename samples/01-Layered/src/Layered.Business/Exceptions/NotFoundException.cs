namespace Layered.Business.Exceptions;

// Kayıt bulunamadı → API katmanında 404'e çevrilir.
public class NotFoundException(string message) : Exception(message);
