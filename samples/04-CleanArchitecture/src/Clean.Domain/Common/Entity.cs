namespace Clean.Domain.Common;

// [Domain Katmanı] DDD "Entity": kimliği (Id) olan ve zamanla değişen nesne.
// İki entity, alanları farklı olsa bile Id'leri aynıysa "aynı" nesnedir.
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : struct
{
    public TId Id { get; protected set; }

    public bool Equals(Entity<TId>? other) =>
        other is not null && other.GetType() == GetType() && other.Id.Equals(Id);

    public override bool Equals(object? obj) => obj is Entity<TId> e && Equals(e);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
