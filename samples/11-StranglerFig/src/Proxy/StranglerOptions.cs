namespace Proxy;

public sealed class StranglerOptions
{
    // Feature flag: true → /api/orders/** modern sisteme, false → legacy'ye (anında rollback).
    public bool OrdersMigrated { get; set; }
    public string LegacyUrl { get; set; } = "http://localhost:5201";
    public string ModernUrl { get; set; } = "http://localhost:5202";
}
