// [Gateway — API Composition istemcileri] Her iç servis için typed HttpClient.
// Hepsi AddStandardResilienceHandler() ile sarılı (Program.cs): rate limiter → total timeout → retry
// → circuit breaker → attempt timeout. Pipeline tükenirse istisna burada yakalanır ve FALLBACK
// olarak "veri yok + uyarı" döner; tek bir servisin çökmesi tüm ekranı çökertmez.
using System.Net;
using System.Text.Json;

namespace Gateway.Api.Composition;

public record DownstreamResult(HttpStatusCode? StatusCode, JsonElement? Body, string? Error)
{
    public bool IsSuccess => Error is null && StatusCode is HttpStatusCode.OK;
    public bool IsNotFound => StatusCode is HttpStatusCode.NotFound;
}

public abstract class DownstreamClient(HttpClient http, ILogger logger)
{
    protected abstract string ServiceName { get; }

    protected async Task<DownstreamResult> GetJsonAsync(string path, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
                return new(response.StatusCode, null, $"{ServiceName} {(int)response.StatusCode} döndü");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: ct);
            return new(response.StatusCode, json, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       or Polly.CircuitBreaker.BrokenCircuitException
                                       or Polly.Timeout.TimeoutRejectedException)
        {
            logger.LogWarning("{Service} çağrısı başarısız ({Path}): {Error}", ServiceName, path, ex.Message);
            return new(null, null, $"{ServiceName} erişilemez: {ex.GetType().Name}");
        }
    }
}

public class OrderingClient(HttpClient http, ILogger<OrderingClient> logger) : DownstreamClient(http, logger)
{
    protected override string ServiceName => "Ordering";
    public Task<DownstreamResult> GetOrderAsync(Guid id, CancellationToken ct) => GetJsonAsync($"/orders/{id}", ct);
    public Task<DownstreamResult> GetSagaAsync(Guid id, CancellationToken ct) => GetJsonAsync($"/orders/{id}/saga", ct);
}

public class PaymentClient(HttpClient http, ILogger<PaymentClient> logger) : DownstreamClient(http, logger)
{
    protected override string ServiceName => "Payment";
    public Task<DownstreamResult> GetPaymentsAsync(Guid orderId, CancellationToken ct) => GetJsonAsync($"/payments?orderId={orderId}", ct);
}

public class InventoryClient(HttpClient http, ILogger<InventoryClient> logger) : DownstreamClient(http, logger)
{
    protected override string ServiceName => "Inventory";
    public Task<DownstreamResult> GetReservationsAsync(Guid orderId, CancellationToken ct) => GetJsonAsync($"/reservations?orderId={orderId}", ct);
}
