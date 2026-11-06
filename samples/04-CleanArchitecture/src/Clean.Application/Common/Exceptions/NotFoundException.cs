namespace Clean.Application.Common.Exceptions;

// API katmanında 404'e çevrilir.
public sealed class NotFoundException(string resource, object key)
    : Exception($"'{resource}' bulunamadı (id: {key}).")
{
    public string Resource { get; } = resource;
    public object Key { get; } = key;
}
