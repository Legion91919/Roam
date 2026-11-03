using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Common.Events;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inventory;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CarRental.Application.Fleet;

/// <summary>
/// The fleet drives inventory. These handlers run BEFORE the commit (pre-commit domain events,
/// dispatched by the unit of work), so the vehicle change and the capacity change are ONE
/// transaction: retiring a car whose capacity is already promised fails and nothing is saved.
/// See docs/ARCHITECTURE.md "Vehicles and inventory" for why this is not eventual.
/// </summary>
public sealed class FleetCapacityHandlers(ICategoryInventoryRepository inventory, TimeProvider clock, ILogger<FleetCapacityHandlers> logger) :
    INotificationHandler<DomainEventRaised<VehicleRegistered>>,
    INotificationHandler<DomainEventRaised<VehicleRetired>>,
    INotificationHandler<DomainEventRaised<VehicleMovedBranch>>
{
    public async Task Handle(DomainEventRaised<VehicleRegistered> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        FleetCapacity.AddCar(await FutureDaysAsync(e.BranchId, e.Category, cancellationToken));
    }

    public async Task Handle(DomainEventRaised<VehicleRetired> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        FleetCapacity.RemoveCar(await FutureDaysAsync(e.BranchId, e.Category, cancellationToken));
    }

    public async Task Handle(DomainEventRaised<VehicleMovedBranch> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        var shortDays = FleetCapacity.RemoveCarWherePossible(await FutureDaysAsync(e.FromBranchId, e.Category, cancellationToken));
        FleetCapacity.AddCar(await FutureDaysAsync(e.ToBranchId, e.Category, cancellationToken));

        if (shortDays.Count > 0)
            logger.LogWarning("One-way return left {Category} at branch {Branch} short on {Days} fully booked day(s) from {First}: move a car back",
                e.Category, e.FromBranchId, shortDays.Count, shortDays[0]);
    }

    /// <summary>Today (UTC) onwards: past days are history and never change.</summary>
    private Task<IReadOnlyList<CategoryInventory>> FutureDaysAsync(BranchId branch, VehicleCategory category, CancellationToken cancellationToken) =>
        inventory.GetRangeAsync(branch, category, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), DateOnly.MaxValue, cancellationToken);
}
