namespace Hexagonal.Core.Ports.In;

// [Inbound / Driving Port]
public interface ICancelOrderUseCase
{
    Task ExecuteAsync(Guid orderId, CancellationToken ct = default);
}
