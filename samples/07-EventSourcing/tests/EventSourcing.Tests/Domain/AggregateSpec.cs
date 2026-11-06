using EventSourcing.Api.Domain;

namespace EventSourcing.Tests.Domain;

// Given(geçmiş event'ler) → When(komut) → Then(beklenen yeni event'ler | hata).
// Event sourced aggregate'lerin doğal test dili: durum değil DAVRANIŞ (üretilen event'ler) doğrulanır.
public sealed class AggregateSpec
{
    private BankAccount? _account;
    private Exception? _error;

    private readonly IEvent[] _history;
    private AggregateSpec(IEvent[] history) => _history = history;

    public static AggregateSpec Given(params IEvent[] history) => new(history);

    public AggregateSpec When(Action<BankAccount> command)
    {
        _account = BankAccount.Rehydrate(_history);
        try { command(_account); }
        catch (Exception ex) { _error = ex; }
        return this;
    }

    public AggregateSpec When(Func<BankAccount> create)
    {
        try { _account = create(); }
        catch (Exception ex) { _error = ex; }
        return this;
    }

    public BankAccount Then(params IEvent[] expected)
    {
        Assert.Null(_error);
        Assert.Equal(expected, _account!.Uncommitted);
        return _account;
    }

    public void ThenThrows<TException>(string messagePart) where TException : Exception
    {
        var ex = Assert.IsType<TException>(_error);
        Assert.Contains(messagePart, ex.Message);
        Assert.Empty(_account?.Uncommitted ?? []);
    }
}
