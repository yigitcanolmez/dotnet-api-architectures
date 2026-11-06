using System.ServiceModel;

namespace Soa.LoanOrchestrator.Api.ServiceReferences;

// ChannelFactory pahalıdır → singleton. Kanal ucuzdur → çağrı başına aç/kapat; hata olursa Abort.
public sealed class SoaChannels(IConfiguration config) : IDisposable
{
    private static BasicHttpBinding Binding() => new()
    {
        SendTimeout = TimeSpan.FromSeconds(10),
        OpenTimeout = TimeSpan.FromSeconds(5)
    };

    private readonly ChannelFactory<ICreditScoringService> _credit =
        new(Binding(), new EndpointAddress(config["SoaServices:CreditScoringUrl"]!));

    private readonly ChannelFactory<ICustomerService> _customer =
        new(Binding(), new EndpointAddress(config["SoaServices:CustomerUrl"]!));

    public Task<T> CreditScoring<T>(Func<ICreditScoringService, Task<T>> call) => Invoke(_credit, call);
    public Task<T> Customer<T>(Func<ICustomerService, Task<T>> call) => Invoke(_customer, call);

    private static async Task<T> Invoke<TChannel, T>(ChannelFactory<TChannel> factory, Func<TChannel, Task<T>> call)
    {
        var channel = factory.CreateChannel();
        var client = (IClientChannel)channel!;
        try
        {
            var result = await call(channel);
            client.Close();
            return result;
        }
        catch
        {
            client.Abort();
            throw;
        }
    }

    public void Dispose()
    {
        _credit.Abort();
        _customer.Abort();
    }
}
