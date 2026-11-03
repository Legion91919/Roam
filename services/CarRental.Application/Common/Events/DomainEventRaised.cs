using CarRental.Domain.Common;
using MediatR;

namespace CarRental.Application.Common.Events;

/// <summary>
/// PRE-COMMIT domain event: published by the unit of work BEFORE SaveChanges, inside the same
/// transaction. Handlers may change other aggregates through repositories, and those changes are
/// saved with the original ones; if a handler throws, nothing is saved. Use it for consistency that
/// must be immediate (fleet capacity). Use <see cref="DomainEventNotification{TDomainEvent}"/>
/// (after commit) for side effects that may lag (read models, emails).
/// </summary>
public sealed record DomainEventRaised<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
