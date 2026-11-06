using Hexagonal.Core.Ports.In;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexagonal.Adapters.Messaging;

// [Driving Adapter: Message Queue] Kuyruktan mesaj okur ve WEB ADAPTER'IN ÇAĞIRDIĞI AYNI inbound port'u
// (IPlaceOrderUseCase) tetikler. Core, isteğin HTTP'den mi kuyruktan mı geldiğini bilmez.
public class PlaceOrderConsumer(
    InMemoryOrderQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<PlaceOrderConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            // Her mesaj kendi DI scope'unda (HTTP isteğindeki scope'un karşılığı).
            using var scope = scopeFactory.CreateScope();
            var placeOrder = scope.ServiceProvider.GetRequiredService<IPlaceOrderUseCase>();

            var command = new PlaceOrderCommand(
                message.CustomerId,
                message.Items.Select(i => new PlaceOrderItem(i.ProductId, i.Quantity)).ToList());

            try
            {
                var orderId = await placeOrder.ExecuteAsync(command, stoppingToken);
                logger.LogInformation("📨 Kuyruk mesajı {MessageId} işlendi → sipariş {OrderId} oluşturuldu.", message.MessageId, orderId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Gerçek bir broker'da burada retry / dead-letter queue olurdu.
                logger.LogWarning("📨 Kuyruk mesajı {MessageId} reddedildi: {Reason}", message.MessageId, ex.Message);
            }
        }
    }
}
