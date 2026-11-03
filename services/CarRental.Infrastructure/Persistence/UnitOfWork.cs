using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Common.Events;
using CarRental.Application.Common.Exceptions;
using CarRental.Domain.Common;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarRental.Infrastructure.Persistence;

/// <summary>
/// ADAPTER for the IUnitOfWork port.
/// </summary>
/// <remarks>
/// 1. Collects domain events from every tracked aggregate BEFORE saving, and publishes them as
///    PRE-COMMIT notifications (DomainEventRaised&lt;T&gt;) whose handlers join this transaction.
/// 2. SaveChanges: EF wraps all INSERT/UPDATEs in one transaction. Rowversion checks happen here.
/// 3. Translates EF/SQL exceptions into the Application's own exception type, so no layer above
///    ever needs to reference EF Core (this is the Adapter pattern doing Dependency Inversion's job).
/// 4. Publishes the events AFTER the commit, so read models never see something that was rolled back.
///
/// Known trade-off: if the process dies between commit and publish, the Mongo
/// projection misses an event. Production fix: the TRANSACTIONAL OUTBOX pattern (write events to an
/// Outbox table in the same transaction, publish from a background worker). Here, the projection can
/// be rebuilt from SQL at any time (AvailabilityProjectionRebuilder), which is an acceptable PoC answer.
/// </remarks>
internal sealed class UnitOfWork(CarRentalDbContext db, IPublisher publisher, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var aggregates = db.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();
        var domainEvents = aggregates.SelectMany(a => a.DomainEvents).ToList();

        // PRE-COMMIT: handlers that must be consistent with this change (fleet capacity) run now, in
        // the same DbContext, so their changes are saved in the SAME transaction below. If one
        // throws, nothing is saved.
        foreach (var domainEvent in domainEvents)
            await publisher.Publish(Wrap(typeof(DomainEventRaised<>), domainEvent), cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The data was changed by another request. Please retry.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 or 1205 })
        {
            // 2601/2627 unique index violation (two checkouts of one reservation racing) or 1205 deadlock
            // victim: same meaning as a concurrency conflict (we lost a race), and equally safe to retry.
            throw new ConcurrencyConflictException("A conflicting change was made by another request.", ex);
        }

        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();

        foreach (var domainEvent in domainEvents)
        {
            try
            {
                await publisher.Publish(Wrap(typeof(DomainEventNotification<>), domainEvent), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The booking IS committed. A failing read-model update must not turn that into a 500.
                logger.LogError(ex, "Publishing {Event} failed after commit; read models can be rebuilt", domainEvent.GetType().Name);
            }
        }
    }

    public void DiscardChanges() => db.ChangeTracker.Clear();

    /// <summary>ReservationCreated -> DomainEventNotification&lt;ReservationCreated&gt; (closed at runtime).</summary>
    private static object Wrap(Type notification, IDomainEvent domainEvent) =>
        Activator.CreateInstance(notification.MakeGenericType(domainEvent.GetType()), domainEvent)!;
}
