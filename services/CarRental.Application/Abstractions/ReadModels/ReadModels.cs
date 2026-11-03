using CarRental.Application.Branches.GetBranches;
using CarRental.Application.Counter;
using CarRental.Application.Fleet;
using CarRental.Application.Inspections;
using CarRental.Application.Customers.GetMyProfile;
using CarRental.Application.Reservations.GetReservationById;
using CarRental.Domain.ValueObjects;

namespace CarRental.Application.Abstractions.ReadModels;

// ---------------------------------------------------------------------------------------------
// PORTS for the READ side (queries). They return flat DTOs, never aggregates.
//
// Queries skip the Domain on purpose: there are no invariants to protect when reading, so loading
// and tracking full aggregates would only cost memory and SQL. Each port can be implemented by
// whatever store reads fastest. IAvailabilityReadModel has TWO implementations
// (SQL AsNoTracking and MongoDB), chosen in configuration.
//
// SOLID (L) Liskov: either implementation can be substituted for the other with no change to the
//           query handler. Both must return the same rows for the same data, which the shared
//           AvailabilityReadModelContract tests check.
// ---------------------------------------------------------------------------------------------

/// <summary>Cars still available for one category at one branch on one day.</summary>
public sealed record CategoryDayAvailability(VehicleCategory Category, DateOnly Date, int Available);

public interface IAvailabilityReadModel
{
    Task<IReadOnlyList<CategoryDayAvailability>> GetDailyAvailabilityAsync(
        Guid branchId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

/// <summary>
/// Reservations as the signed-in customer may see them. Ownership is part of the QUERY (filtered in
/// SQL by the user id), so another customer's reservation is simply "not found", never leaked.
/// </summary>
public interface IReservationReadModel
{
    Task<ReservationDetailsDto?> GetForUserAsync(Guid reservationId, string userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReservationDetailsDto>> ListForUserAsync(string userId, CancellationToken cancellationToken);
}

public interface ICustomerReadModel
{
    Task<CustomerProfileDto?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);
}

public interface IBranchReadModel
{
    Task<IReadOnlyList<BranchDto>> GetAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The counter's view: ANY customer's bookings at a branch (staff only, enforced by the API's role
/// check). Separate from IReservationReadModel, whose every query is filtered by the signed-in owner.
/// </summary>
public interface ICounterReadModel
{
    /// <summary>Confirmed and checked-out bookings picking up at the branch in [from, to).</summary>
    Task<IReadOnlyList<CounterBookingDto>> ListPickupsAsync(Guid branchId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    /// <summary>Open rentals returning to the branch and due before <paramref name="dueBefore"/>.</summary>
    Task<IReadOnlyList<CounterBookingDto>> ListOpenReturnsAsync(Guid branchId, DateTimeOffset dueBefore, CancellationToken cancellationToken);

    Task<CounterBookingDto?> FindAsync(Guid reservationId, CancellationToken cancellationToken);

    /// <summary>By the 16 hex digits of a booking reference. Null when none (or, improbably, more than one) match.</summary>
    Task<CounterBookingDto?> FindByReferencePrefixAsync(string hexPrefix, CancellationToken cancellationToken);
}

/// <summary>The fleet as staff see it: list, details, history timeline, cars free for a handover.</summary>
public interface IFleetReadModel
{
    Task<PagedResult<VehicleListItemDto>> ListAsync(VehicleFilter filter, CancellationToken cancellationToken);
    Task<VehicleDto?> GetAsync(Guid vehicleId, CancellationToken cancellationToken);
    Task<VehicleHistoryDto?> GetHistoryAsync(Guid vehicleId, CancellationToken cancellationToken);

    /// <summary>Available cars at the branch in <paramref name="category"/> (and higher categories when <paramref name="includeUpgrades"/>).</summary>
    Task<IReadOnlyList<AvailableVehicleDto>> ListAvailableAsync(Guid branchId, VehicleCategory category, bool includeUpgrades, CancellationToken cancellationToken);
}

/// <summary>Inspection reports for display (staff and customers). Never returns storage keys to the client.</summary>
public interface IInspectionReadModel
{
    Task<InspectionDto?> GetAsync(Guid inspectionId, CancellationToken cancellationToken);

    /// <summary>COMPLETED reports for a booking, OWNED by the user (filtered in SQL). Others' bookings: empty.</summary>
    Task<IReadOnlyList<InspectionDto>> ListCompletedForOwnerAsync(Guid reservationId, string userId, CancellationToken cancellationToken);

    /// <summary>All reports for a booking (staff).</summary>
    Task<IReadOnlyList<InspectionSummaryDto>> ListForReservationAsync(Guid reservationId, CancellationToken cancellationToken);

    /// <summary>What the download endpoint needs to decide access and stream the file.</summary>
    Task<AttachmentAccess?> GetAttachmentAsync(Guid inspectionId, Guid attachmentId, CancellationToken cancellationToken);
}

public sealed record AttachmentAccess(string StorageKey, string ContentType, string FileName, bool Completed, string OwnerUserId);
