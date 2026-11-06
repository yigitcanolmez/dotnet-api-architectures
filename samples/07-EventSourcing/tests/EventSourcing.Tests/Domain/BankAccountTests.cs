using EventSourcing.Api.Domain;
using static EventSourcing.Tests.Domain.AggregateSpec;

namespace EventSourcing.Tests.Domain;

public sealed class BankAccountTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly AccountOpened Opened = new(Id, "Ali");

    [Fact]
    public void Hesap_acilir() =>
        Given()
            .When(() => BankAccount.Open(Id, "Ali"))
            .Then(new AccountOpened(Id, "Ali"));

    [Fact]
    public void Para_yatirilir()
    {
        var account = Given(Opened)
            .When(a => a.Deposit(200))
            .Then(new MoneyDeposited(Id, 200));

        Assert.Equal(200, account.Balance);
        Assert.Equal(2, account.Version);
        Assert.Equal(1, account.PersistedVersion);
    }

    [Fact]
    public void Yeterli_bakiye_varsa_para_cekilir() =>
        Given(Opened, new MoneyDeposited(Id, 200))
            .When(a => a.Withdraw(150))
            .Then(new MoneyWithdrawn(Id, 150));

    [Fact]
    public void Yetersiz_bakiye_ile_para_cekilemez() =>
        Given(Opened, new MoneyDeposited(Id, 200), new MoneyWithdrawn(Id, 100))
            .When(a => a.Withdraw(101))
            .ThenThrows<DomainException>("Yetersiz bakiye");

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Tutar_sifirdan_buyuk_olmali(decimal amount) =>
        Given(Opened)
            .When(a => a.Deposit(amount))
            .ThenThrows<DomainException>("0'dan büyük");

    [Fact]
    public void Hesap_kapatilir() =>
        Given(Opened, new MoneyDeposited(Id, 50))
            .When(a => a.Close())
            .Then(new AccountClosed(Id));

    [Fact]
    public void Kapali_hesaba_para_yatirilamaz() =>
        Given(Opened, new AccountClosed(Id))
            .When(a => a.Deposit(10))
            .ThenThrows<DomainException>("Kapalı hesap");

    [Fact]
    public void Kapali_hesap_tekrar_kapatilamaz() =>
        Given(Opened, new AccountClosed(Id))
            .When(a => a.Close())
            .ThenThrows<DomainException>("Kapalı hesap");

    [Fact]
    public void Snapshot_ve_kalan_eventler_ayni_durumu_uretir()
    {
        IEvent[] history = [Opened, new MoneyDeposited(Id, 100), new MoneyWithdrawn(Id, 30), new MoneyDeposited(Id, 5)];

        var full = BankAccount.Rehydrate(history);
        var snapshot = BankAccount.Rehydrate(history.Take(3)).ToSnapshot();
        var fromSnapshot = BankAccount.FromSnapshot(snapshot, history.Skip(3));

        Assert.Equal(full.ToSnapshot(), fromSnapshot.ToSnapshot());
        Assert.Equal(75, fromSnapshot.Balance);
        Assert.Equal(4, fromSnapshot.Version);
    }
}
