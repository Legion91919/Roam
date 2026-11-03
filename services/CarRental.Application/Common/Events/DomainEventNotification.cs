using CarRental.Domain.Common;
using MediatR;

namespace CarRental.Application.Common.Events;

/// <summary>
/// Adapter that lets a pure domain event travel through MediatR.
/// </summary>
/// <remarks>
/// The Domain must not reference MediatR, so IDomainEvent cannot implement INotification.
/// Instead Infrastructure wraps each event in this generic notification after a successful save,
/// and handlers subscribe to, e.g., INotificationHandler&lt;DomainEventNotification&lt;ReservationCreated&gt;&gt;.
/// (This is the Adapter pattern keeping the Dependency Rule intact.)
/// </remarks>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
