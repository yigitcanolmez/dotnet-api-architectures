namespace Cqrs.Api.Projections;

public sealed class ProjectionOptions
{
    // Eventual consistency'yi gözlemlenebilir kılmak için her event'ten önce KASITLI gecikme.
    public int DelayMs { get; set; } = 500;
}
