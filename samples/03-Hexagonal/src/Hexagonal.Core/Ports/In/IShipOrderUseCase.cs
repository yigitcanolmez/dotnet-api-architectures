namespace Hexagonal.Core.Ports.In;

// [Inbound / Driving Port]
public interface IShipOrderUseCase
{
    Task ExecuteAsync(Guid orderId, CancellationToken ct = default);
}
