using System.Collections.Concurrent;

namespace ResilienceClient.Api.Infrastructure;

// Fallback için "son bilinen iyi değer" önbelleği. Canlı çağrı başarılı oldukça güncellenir.
public sealed class LastKnownPriceCache
{
    private readonly ConcurrentDictionary<Guid, PriceDto> _prices = new();
    public void Set(PriceDto p) => _prices[p.ProductId] = p;
    public PriceDto? Get(Guid id) => _prices.TryGetValue(id, out var p) ? p : null;
}
