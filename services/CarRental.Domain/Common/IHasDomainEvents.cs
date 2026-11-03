namespace CarRental.Domain.Common;

/// <summary>
/// Non-generic view of an aggregate's pending events, so Infrastructure can find every changed
/// aggregate in the change tracker without knowing its concrete type or ID type.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
