namespace Cqrs.Api.Common;

// [Common] MediatR yerine kendi minik arayüzlerimiz. CQRS ≠ MediatR:
// önemli olan komut ve sorgunun AYRI tipler ve AYRI kod yolları olması.

// Komut: durumu değiştirir, veri DÖNDÜRMEZ (void)...
public interface ICommandHandler<in TCommand>
{
    Task HandleAsync(TCommand command, CancellationToken ct);
}

// ...ya da en fazla oluşturulan kaydın kimliğini döner.
public interface ICommandHandler<in TCommand, TId>
{
    Task<TId> HandleAsync(TCommand command, CancellationToken ct);
}

// Sorgu: veri döner, durumu ASLA değiştirmez (güvenle cache'lenebilir/tekrarlanabilir).
public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct);
}
