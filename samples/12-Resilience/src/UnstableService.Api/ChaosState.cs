namespace UnstableService.Api;

// Çalışma zamanında değiştirilebilen arıza ayarları.
//   healthy → hata yok, gecikme yok
//   flaky   → FailureRate olasılıkla 500 (varsayılan %50)
//   down    → her istek 503
//   slow    → LatencyRate olasılıkla LatencyMs gecikme (varsayılan 3000 ms, %100)
public sealed record ChaosSettings(string Mode = "healthy", double FailureRate = 0, int LatencyMs = 0, double LatencyRate = 1.0);

public sealed record ChaosRequest(string? Mode, double? FailureRate, int? LatencyMs, double? LatencyRate);

public sealed class ChaosState
{
    private ChaosSettings _settings;
    private long _total, _succeeded, _failed, _cancelled;
    private int _inFlight, _maxConcurrent;

    public ChaosState(IConfiguration config) =>
        _settings = Normalize(config.GetSection("Chaos").Get<ChaosSettings>() ?? new ChaosSettings());

    public ChaosSettings Settings => Volatile.Read(ref _settings);

    public ChaosSettings Apply(ChaosRequest r)
    {
        var mode = (r.Mode ?? Settings.Mode).ToLowerInvariant();
        // Mod seçilince önce o modun varsayılanları, sonra açıkça verilen değerler uygulanır.
        var preset = mode switch
        {
            "healthy" => new ChaosSettings("healthy", 0, 0, 1),
            "flaky" => new ChaosSettings("flaky", 0.5, 0, 1),
            "down" => new ChaosSettings("down", 1, 0, 1),
            "slow" => new ChaosSettings("slow", 0, 3000, 1),
            _ => throw new ArgumentException($"Geçersiz mod: {mode}. (healthy|flaky|down|slow)")
        };
        var next = Normalize(preset with
        {
            FailureRate = r.FailureRate ?? preset.FailureRate,
            LatencyMs = r.LatencyMs ?? preset.LatencyMs,
            LatencyRate = r.LatencyRate ?? preset.LatencyRate
        });
        Volatile.Write(ref _settings, next);
        return next;
    }

    private static ChaosSettings Normalize(ChaosSettings s) => s with
    {
        Mode = s.Mode.ToLowerInvariant(),
        FailureRate = Math.Clamp(s.FailureRate, 0, 1),
        LatencyMs = Math.Max(0, s.LatencyMs),
        LatencyRate = Math.Clamp(s.LatencyRate, 0, 1)
    };

    public long BeginRequest()
    {
        var n = Interlocked.Increment(ref _total);
        var now = Interlocked.Increment(ref _inFlight);
        int max;
        while (now > (max = Volatile.Read(ref _maxConcurrent)) && Interlocked.CompareExchange(ref _maxConcurrent, now, max) != max) { }
        return n;
    }

    public void EndRequest(bool? success)
    {
        Interlocked.Decrement(ref _inFlight);
        if (success is null) Interlocked.Increment(ref _cancelled);
        else if (success.Value) Interlocked.Increment(ref _succeeded);
        else Interlocked.Increment(ref _failed);
    }

    public object Stats() => new
    {
        totalRequests = Interlocked.Read(ref _total),
        succeeded = Interlocked.Read(ref _succeeded),
        failed = Interlocked.Read(ref _failed),
        cancelledByClient = Interlocked.Read(ref _cancelled),
        inFlight = Volatile.Read(ref _inFlight),
        maxConcurrent = Volatile.Read(ref _maxConcurrent),
        chaos = Settings
    };

    public void Reset()
    {
        Interlocked.Exchange(ref _total, 0);
        Interlocked.Exchange(ref _succeeded, 0);
        Interlocked.Exchange(ref _failed, 0);
        Interlocked.Exchange(ref _cancelled, 0);
        Interlocked.Exchange(ref _maxConcurrent, Volatile.Read(ref _inFlight));
    }
}
