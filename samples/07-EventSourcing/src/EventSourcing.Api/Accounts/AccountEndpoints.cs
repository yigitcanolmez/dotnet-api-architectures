using System.Text.Json.Nodes;
using EventSourcing.Api.Domain;
using EventSourcing.Api.EventStore;
using EventSourcing.Api.Projections;

namespace EventSourcing.Api.Accounts;

// [API] Komutlar: yükle (event'lerden) → karar ver (aggregate) → yeni event'leri append et.
// Sorgular: ya aggregate'i rehydrate eder (tek hesap, güçlü tutarlılık), ya da projeksiyondan okur (liste).
public static class AccountEndpoints
{
    public sealed record OpenAccountRequest(string Owner);
    public sealed record AmountRequest(decimal Amount, int? ExpectedVersion = null);
    public sealed record CloseRequest(int? ExpectedVersion = null);
    public sealed record AccountResponse(Guid Id, string Owner, decimal Balance, bool IsClosed, int Version, int? LoadedFromSnapshotVersion);
    public sealed record EventResponse(long GlobalPosition, int Version, string Type, JsonNode? Data, DateTime Timestamp);
    public sealed record BalanceAtResponse(Guid Id, DateTime Timestamp, decimal Balance, bool IsClosed, int Version);

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/accounts").WithTags("Accounts");

        accounts.MapPost("/", async (OpenAccountRequest request, BankAccountRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Owner))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["owner"] = ["Hesap sahibi boş olamaz."] });

            var account = BankAccount.Open(Guid.NewGuid(), request.Owner);
            await repo.SaveAsync(account, expectedVersion: 0, ct);
            return Results.Created($"/accounts/{account.Id}", new { id = account.Id });
        });

        accounts.MapPost("/{id:guid}/deposit", (Guid id, AmountRequest request, BankAccountRepository repo, CancellationToken ct) =>
            Execute(id, request.Amount, request.ExpectedVersion, (a, amount) => a.Deposit(amount), repo, ct));

        accounts.MapPost("/{id:guid}/withdraw", (Guid id, AmountRequest request, BankAccountRepository repo, CancellationToken ct) =>
            Execute(id, request.Amount, request.ExpectedVersion, (a, amount) => a.Withdraw(amount), repo, ct));

        accounts.MapPost("/{id:guid}/close", async (Guid id, CloseRequest? request, BankAccountRepository repo, CancellationToken ct) =>
        {
            if (await repo.LoadAsync(id, ct) is not { } loaded) return NotFound(id);
            loaded.Account.Close();
            await repo.SaveAsync(loaded.Account, request?.ExpectedVersion, ct);
            return Results.NoContent();
        });

        // Güncel durum: aggregate event'lerden (varsa snapshot + kalan event'ler) yeniden oluşturulur.
        accounts.MapGet("/{id:guid}", async (Guid id, BankAccountRepository repo, CancellationToken ct) =>
            await repo.LoadAsync(id, ct) is { } loaded
                ? Results.Ok(new AccountResponse(loaded.Account.Id, loaded.Account.Owner, loaded.Account.Balance,
                    loaded.Account.IsClosed, loaded.Account.Version, loaded.SnapshotVersion))
                : NotFound(id));

        // Audit: ham event akışı. "Bu bakiye neden 150?" sorusunun cevabı.
        accounts.MapGet("/{id:guid}/events", async (Guid id, IEventStore store, CancellationToken ct) =>
        {
            var events = await store.ReadStreamAsync(BankAccountRepository.StreamId(id), ct: ct);
            return events.Count == 0
                ? NotFound(id)
                : Results.Ok(events.Select(e => new EventResponse(e.GlobalPosition, e.Version, e.Type, JsonNode.Parse(e.Data), e.Timestamp)));
        });

        // Temporal query: belirtilen andaki durum = o ana kadarki event'lerin replay'i.
        accounts.MapGet("/{id:guid}/balance-at", async (Guid id, DateTimeOffset timestamp, IEventStore store, CancellationToken ct) =>
        {
            var events = await store.ReadStreamAsync(BankAccountRepository.StreamId(id), timestamp.UtcDateTime, ct: ct);
            if (events.Count == 0)
                return Results.Problem(statusCode: 404, title: "NotFound", detail: $"{timestamp:O} anında hesap henüz yoktu ya da bulunamadı.");

            var account = BankAccount.Rehydrate(events.Select(e => e.Event));
            return Results.Ok(new BalanceAtResponse(id, timestamp.UtcDateTime, account.Balance, account.IsClosed, account.Version));
        });

        // Liste: projeksiyondan (eventually consistent — worker birkaç yüz ms geriden gelebilir).
        accounts.MapGet("/", async (AccountBalanceProjection projection, CancellationToken ct) =>
            Results.Ok(await projection.ListAsync(ct)));

        app.MapPost("/admin/projections/rebuild", async (AccountBalanceProjection projection, CancellationToken ct) =>
            Results.Ok(new { replayedEvents = await projection.RebuildAsync(ct) }))
            .WithTags("Admin");

        return app;
    }

    private static async Task<IResult> Execute(Guid id, decimal amount, int? expectedVersion,
        Action<BankAccount, decimal> command, BankAccountRepository repo, CancellationToken ct)
    {
        if (amount <= 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["amount"] = ["Tutar 0'dan büyük olmalı."] });
        if (await repo.LoadAsync(id, ct) is not { } loaded)
            return NotFound(id);

        command(loaded.Account, amount);                         // karar (DomainException → 409)
        await repo.SaveAsync(loaded.Account, expectedVersion, ct); // append (ConcurrencyException → 409)
        return Results.Ok(new { id, balance = loaded.Account.Balance, version = loaded.Account.Version });
    }

    private static IResult NotFound(Guid id) =>
        Results.Problem(statusCode: 404, title: "NotFound", detail: $"Hesap bulunamadı: {id}");
}
