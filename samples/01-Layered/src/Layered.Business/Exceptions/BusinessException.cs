namespace Layered.Business.Exceptions;

// İş kuralı ihlali → API katmanında 409 Conflict'e çevrilir.
public class BusinessException(string message) : Exception(message);
