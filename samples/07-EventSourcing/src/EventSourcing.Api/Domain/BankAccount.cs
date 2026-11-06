namespace EventSourcing.Api.Domain;

// [Domain / Aggregate] Durum (Balance, IsClosed) SAKLANMAZ; event'lerden türetilir.
// Kalıp: komut metodu → kural kontrolü (karar) → Raise(event) → Apply(event) ile durum değişir.
// Apply ASLA kural kontrolü yapmaz: geçmişte olmuş bir olay reddedilemez.
public sealed class BankAccount
{
    private readonly List<IEvent> _uncommitted = [];

    private BankAccount() { }

    public Guid Id { get; private set; }
    public string Owner { get; private set; } = "";
    public decimal Balance { get; private set; }
    public bool IsClosed { get; private set; }

    // Uygulanan event sayısı (= stream'deki son event'in sıra numarası). Optimistic concurrency için.
    public int Version { get; private set; }

    public IReadOnlyList<IEvent> Uncommitted => _uncommitted;

    // Event store'a yazarken beklenen versiyon: yeni event'lerden ÖNCEKİ versiyon.
    public int PersistedVersion => Version - _uncommitted.Count;

    // ── Komutlar (karar verir, event üretir) ─────────────────────────────

    public static BankAccount Open(Guid id, string owner)
    {
        if (string.IsNullOrWhiteSpace(owner))
            throw new DomainException("Hesap sahibi boş olamaz.");

        var account = new BankAccount();
        account.Raise(new AccountOpened(id, owner.Trim()));
        return account;
    }

    public void Deposit(decimal amount)
    {
        EnsureOpen();
        EnsurePositive(amount);
        Raise(new MoneyDeposited(Id, amount));
    }

    public void Withdraw(decimal amount)
    {
        EnsureOpen();
        EnsurePositive(amount);
        if (amount > Balance)
            throw new DomainException($"Yetersiz bakiye. Bakiye: {Balance}, istenen: {amount}.");
        Raise(new MoneyWithdrawn(Id, amount));
    }

    public void Close()
    {
        EnsureOpen();
        Raise(new AccountClosed(Id));
    }

    // ── Durum (SADECE Apply ile değişir) ─────────────────────────────────

    private void Apply(IEvent e)
    {
        switch (e)
        {
            case AccountOpened x: Id = x.AccountId; Owner = x.Owner; break;
            case MoneyDeposited x: Balance += x.Amount; break;
            case MoneyWithdrawn x: Balance -= x.Amount; break;
            case AccountClosed: IsClosed = true; break;
            default: throw new InvalidOperationException($"Bilinmeyen event: {e.GetType().Name}");
        }
        Version++;
    }

    private void Raise(IEvent e)
    {
        Apply(e);
        _uncommitted.Add(e);
    }

    public void MarkCommitted() => _uncommitted.Clear();

    // ── Yeniden oluşturma (rehydration) ──────────────────────────────────

    public static BankAccount Rehydrate(IEnumerable<IEvent> history) =>
        FromSnapshot(null, history);

    // Snapshot varsa ondan başla, sadece sonraki event'leri oynat.
    public static BankAccount FromSnapshot(BankAccountSnapshot? snapshot, IEnumerable<IEvent> eventsAfter)
    {
        var account = new BankAccount();
        if (snapshot is not null)
        {
            account.Id = snapshot.Id;
            account.Owner = snapshot.Owner;
            account.Balance = snapshot.Balance;
            account.IsClosed = snapshot.IsClosed;
            account.Version = snapshot.Version;
        }

        foreach (var e in eventsAfter)
            account.Apply(e);
        return account;
    }

    public BankAccountSnapshot ToSnapshot() => new(Id, Owner, Balance, IsClosed, Version);

    private void EnsureOpen()
    {
        if (IsClosed) throw new DomainException("Kapalı hesapta işlem yapılamaz.");
    }

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0) throw new DomainException("Tutar 0'dan büyük olmalı.");
    }
}

// Belirli bir versiyondaki durumun donmuş kopyası (bkz. Snapshots/).
public sealed record BankAccountSnapshot(Guid Id, string Owner, decimal Balance, bool IsClosed, int Version);
