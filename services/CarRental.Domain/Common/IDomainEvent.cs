namespace CarRental.Domain.Common;

/// <summary>
/// Something important that already happened in the domain ("ReservationCreated").
/// Events are named in the past tense and are immutable facts.
/// </summary>
/// <remarks>
/// The Domain only DEFINES events. It never publishes them itself, because publishing needs a
/// mediator or message bus, and the Domain may not reference one. Infrastructure collects the
/// events after a successful save and dispatches them (see UnitOfWork).
/// </remarks>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
