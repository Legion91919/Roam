namespace CarRental.Domain.Common;

/// <summary>
/// An entity has an identity that stays the same while its data changes
/// (a reservation is the same reservation after it is cancelled).
/// Two entities are equal when their IDs are equal, not when their fields are.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : struct
{
    public TId Id { get; protected init; }

    protected Entity(TId id) => Id = id;

    /// <summary>Required by EF Core to materialise objects. Never used by application code.</summary>
    protected Entity() { }

    public bool Equals(Entity<TId>? other) =>
        other is not null && other.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
