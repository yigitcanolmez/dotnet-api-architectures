using Cqrs.Api.Write.Domain;

namespace Cqrs.Api.Projections;

// Bir projektör, ilgilendiği event'leri okuma modeline yansıtır. İdempotent yazılmalı
// (aynı event iki kez gelirse sonuç değişmemeli) — broker'lar "en az bir kez" teslim eder.
public interface IProjector
{
    Task ProjectAsync(IDomainEvent domainEvent, CancellationToken ct);
}
