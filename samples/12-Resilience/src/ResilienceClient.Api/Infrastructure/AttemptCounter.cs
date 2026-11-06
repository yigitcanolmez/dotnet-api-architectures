namespace ResilienceClient.Api.Infrastructure;

// Öğretim amaçlı: tek bir mantıksal isteğin downstream'e kaç DENEME (attempt) olarak gittiğini sayar.
// Resilience handler'dan SONRA (iç tarafa) eklenir; böylece her retry/hedge denemesi buradan geçer.
// AsyncLocal, endpoint'te başlatılan sayacı paralel hedging denemelerine de taşır.
public static class AttemptCounter
{
    private static readonly AsyncLocal<StrongBox> Current = new();

    public sealed class StrongBox { public int Value; }

    public static StrongBox Begin() => Current.Value = new StrongBox();

    public static void Increment()
    {
        if (Current.Value is { } box) Interlocked.Increment(ref box.Value);
    }
}

public sealed class AttemptCountingHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        AttemptCounter.Increment();
        return base.SendAsync(request, ct);
    }
}
