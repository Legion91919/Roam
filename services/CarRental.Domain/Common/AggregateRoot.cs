namespace CarRental.Domain.Common;

/// <summary>
/// An aggregate is a cluster of objects that must stay consistent together; the root is the only
/// entry point. Outside code may hold a reference to the root only, and every change goes through
/// the root's methods, so the root can protect its invariants (business rules that must ALWAYS hold).
/// </summary>
/// <remarks>
/// Design choices:
///  * No public setters on aggregates. State changes only through intention-revealing methods
///    (Cancel, Return, Reserve) that validate first.
///  * One aggregate per transaction is the ideal. CreateReservation touches the Reservation plus the
///    CategoryInventory rows for each day: that is a deliberate, documented exception, because
///    "never overbook" must be atomic. See docs/ARCHITECTURE.md.
///  * Concurrency tokens (rowversion) are NOT declared here. They are a persistence concern and are
///    added as EF Core shadow properties in Infrastructure, keeping the Domain persistence-ignorant.
/// </remarks>
public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents
    where TId : struct
{
    private readonly List<IDomainEvent> _domainEvents = new();

    protected AggregateRoot(TId id) : base(id) { }
    protected AggregateRoot() { }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
