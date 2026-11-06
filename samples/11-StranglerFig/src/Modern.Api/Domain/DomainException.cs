namespace Modern.Api.Domain;

public class DomainException(string message, bool isConflict = false) : Exception(message)
{
    // true → 409 (durum kuralı ihlali), false → 400 (geçersiz girdi)
    public bool IsConflict { get; } = isConflict;
}
