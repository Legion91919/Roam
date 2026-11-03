using CarRental.Domain.Customers;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using CarRental.Domain.Inventory;
using CarRental.Domain.Payments;
using CarRental.Domain.Rentals;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;

namespace CarRental.Application.Abstractions.Persistence;

// ---------------------------------------------------------------------------------------------
// PORTS for the WRITE side (commands). Implemented by Infrastructure (EF Core).
//
// SOLID (D) Dependency Inversion: the Application layer OWNS these interfaces and the handlers
//           depend on them. Infrastructure depends on Application to implement them, so the arrow
//           points inwards: SQL Server could be swapped for PostgreSQL without editing a handler.
// SOLID (I) Interface Segregation: one small repository per aggregate, holding only what the
//           commands need. There is no generic IRepository<T> with 15 methods nobody uses, and no
//           query methods here (reads have their own ports in ReadModels/). Saving is a separate
//           port (IUnitOfWork), so a handler that only reads an aggregate cannot commit.
// Repositories return AGGREGATES (whole consistency boundaries), never IQueryable, so EF Core
// does not leak into the Application layer.
// ---------------------------------------------------------------------------------------------

public interface IReservationRepository
{
    Task<Reservation?> GetAsync(ReservationId id, CancellationToken cancellationToken);
    void Add(Reservation reservation);
}

public interface ICustomerRepository
{
    Task<Customer?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);
    /// <summary>True if another customer already registered this identity document (one profile per person).</summary>
    Task<bool> DocumentInUseAsync(IdentityDocument document, CustomerId? exceptCustomer, CancellationToken cancellationToken);
    void Add(Customer customer);
}

public interface IBranchRepository
{
    Task<bool> ExistsAsync(BranchId id, CancellationToken cancellationToken);
}

public interface IRentalRepository
{
    Task<Rental?> GetAsync(RentalId id, CancellationToken cancellationToken);
    Task<Rental?> GetByReservationAsync(ReservationId reservationId, CancellationToken cancellationToken);
    void Add(Rental rental);
}

public interface IVehicleRepository
{
    Task<Vehicle?> GetAsync(VehicleId id, CancellationToken cancellationToken);
    /// <summary>Plates and VINs are unique across the whole fleet, retired cars included.</summary>
    Task<bool> RegistrationInUseAsync(Registration registration, CancellationToken cancellationToken);
    Task<bool> VinInUseAsync(Vin vin, CancellationToken cancellationToken);
    void Add(Vehicle vehicle);
}

public interface IInspectionRepository
{
    Task<InspectionReport?> GetAsync(InspectionId id, CancellationToken cancellationToken);
    /// <summary>The (at most one) report of this kind for a booking.</summary>
    Task<InspectionReport?> GetAsync(ReservationId reservationId, InspectionKind kind, CancellationToken cancellationToken);
    void Add(InspectionReport report);
}

public interface IPaymentRepository
{
    /// <summary>The payment for a reservation, tracked for update (at most one: unique index on ReservationId).</summary>
    Task<Payment?> GetByReservationAsync(ReservationId reservationId, CancellationToken cancellationToken);
    void Add(Payment payment);
}

public interface ICategoryInventoryRepository
{
    /// <summary>All inventory rows for one branch and category between two dates (inclusive), tracked for update.</summary>
    Task<IReadOnlyList<CategoryInventory>> GetRangeAsync(
        BranchId branchId, VehicleCategory category, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

/// <summary>
/// Commits everything changed in this request as ONE database transaction, then publishes the
/// domain events raised by the changed aggregates.
/// </summary>
public interface IUnitOfWork
{
    /// <exception cref="Common.Exceptions.ConcurrencyConflictException">
    /// Another request changed the same rows first (optimistic concurrency token mismatch).
    /// </exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Forget all loaded and pending changes so a retry starts from fresh data.</summary>
    void DiscardChanges();
}
