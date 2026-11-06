using Yarp.ReverseProxy.Configuration;

namespace Proxy;

// [Strangler Fig — Yönlendirme Katmanı] YARP route/cluster tablosunu feature flag'e göre KODDAN üretir.
// Yeni bir özellik taşındığında buraya bir route (ve gerekirse flag) eklenir; legacy'ye dokunulmaz.
public static class StranglerRouteTable
{
    public const string Legacy = "legacy";
    public const string Modern = "modern";

    public static IReadOnlyList<RouteConfig> BuildRoutes(StranglerOptions o) =>
    [
        // Taşınan dilim: Orders. Order=1 → catch-all'dan ÖNCE değerlendirilir.
        Route("orders", o.OrdersMigrated ? Modern : Legacy, "/api/orders/{**rest}", order: 1),

        // Geri kalan HER ŞEY (henüz taşınmamış özellikler: /api/products, /api/customers ...) legacy'ye.
        Route("legacy-catch-all", Legacy, "{**catch-all}", order: 1000)
    ];

    public static IReadOnlyList<ClusterConfig> BuildClusters(StranglerOptions o) =>
    [
        Cluster(Legacy, o.LegacyUrl),
        Cluster(Modern, o.ModernUrl)
    ];

    private static RouteConfig Route(string id, string clusterId, string path, int order) => new()
    {
        RouteId = id,
        ClusterId = clusterId,
        Order = order,
        Match = new RouteMatch { Path = path },
        Transforms =
        [
            // Öğrenci hangi sistemin yanıt verdiğini görebilsin diye (hata yanıtlarında da: When=Always).
            new Dictionary<string, string> { ["ResponseHeader"] = "X-Served-By", ["Set"] = clusterId, ["When"] = "Always" },
            new Dictionary<string, string> { ["ResponseHeader"] = "X-Strangler-Route", ["Set"] = id, ["When"] = "Always" }
        ]
    };

    private static ClusterConfig Cluster(string id, string address) => new()
    {
        ClusterId = id,
        Destinations = new Dictionary<string, DestinationConfig> { [$"{id}-1"] = new() { Address = address } }
    };
}
