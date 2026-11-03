using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Branches.GetBranches;
using CarRental.Application.Customers.GetMyProfile;
using CarRental.Application.Payments;
using CarRental.Application.Reservations.GetReservationById;
using CarRental.Domain.Identifiers;
using CarRental.Domain.ValueObjects;
using CarRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.ReadModels.Sql;

// ---------------------------------------------------------------------------------------------
// READ SIDE on SQL Server. Two styles, both skipping the Domain:
//  * LINQ projection + AsNoTracking: type-safe, selects only the columns the DTO needs,
//    no change tracking, no aggregate materialisation.
//  * Raw SQL via Database.SqlQuery<T> ("Dapper-style"): hand-written SQL for a hot path, mapped
//    straight to a row type. Interpolated values become SQL PARAMETERS (no injection risk).
// ---------------------------------------------------------------------------------------------

internal sealed class SqlReservationReadModel(CarRentalDbContext db) : IReservationReadModel
{
    public async Task<ReservationDetailsDto?> GetForUserAsync(Guid reservationId, string userId, CancellationToken cancellationToken)
    {
        var id = new ReservationId(reservationId);
        var rows = await Query(userId).Where(r => r.Id == id).ToListAsync(cancellationToken);
        var payments = await PaymentSummaries.ForAsync(db, rows.Select(r => r.Id), cancellationToken);
        var reports = await CompletedReportsAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        return rows.Select(r => ToDto(r, payments, reports)).SingleOrDefault();
    }

    public async Task<IReadOnlyList<ReservationDetailsDto>> ListForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var rows = await Query(userId).OrderByDescending(r => r.PickupAt).Take(50).ToListAsync(cancellationToken);
        var payments = await PaymentSummaries.ForAsync(db, rows.Select(r => r.Id), cancellationToken);
        var reports = await CompletedReportsAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        return rows.Select(r => ToDto(r, payments, reports)).ToList();
    }

    /// <summary>Bookings that have at least one COMPLETED inspection report (the "Report available" pill).</summary>
    private async Task<HashSet<ReservationId>> CompletedReportsAsync(List<ReservationId> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return new();
        return (await db.Inspections.AsNoTracking()
                .Where(i => ids.Contains(i.ReservationId) && i.Status == Domain.Inspections.InspectionStatus.Completed)
                .Select(i => i.ReservationId).Distinct().ToListAsync(cancellationToken))
            .ToHashSet();
    }

    /// <summary>Ownership is part of the SQL: the join on Customers filters by the signed-in user id.</summary>
    private IQueryable<Row> Query(string userId) =>
        from r in db.Reservations.AsNoTracking()
        join customer in db.Customers.AsNoTracking() on r.CustomerId equals customer.Id
        join pickup in db.Branches.AsNoTracking() on r.PickupBranchId equals pickup.Id
        join dropOff in db.Branches.AsNoTracking() on r.ReturnBranchId equals dropOff.Id
        where customer.UserId == userId
        select new Row
        {
            Id = r.Id,
            Category = r.Category,
            Status = r.Status,
            PickupBranchId = r.PickupBranchId,
            PickupBranchName = pickup.Name,
            ReturnBranchId = r.ReturnBranchId,
            ReturnBranchName = dropOff.Name,
            PickupAt = r.Period.PickupAt,
            ReturnAt = r.Period.ReturnAt,
            Amount = r.QuotedTotal.Amount,
            Currency = r.QuotedTotal.Currency,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            TermsVersion = r.Terms.Version,
            CreatedAt = r.CreatedAt,
            CancelledBy = r.Cancellation == null ? null : r.Cancellation.By,
            CancellationReason = r.Cancellation == null ? null : r.Cancellation.Reason,
            CancellationAt = r.Cancellation == null ? null : r.Cancellation.At,
            PaymentOption = r.PaymentOption,
        };

    private static ReservationDetailsDto ToDto(Row row, IReadOnlyDictionary<ReservationId, PaymentSummaryDto> payments, HashSet<ReservationId> reports) => new(
        row.Id.Value, row.Category.ToString(), row.Status.ToString(),
        row.PickupBranchId.Value, row.PickupBranchName, row.ReturnBranchId.Value, row.ReturnBranchName,
        row.PickupAt, row.ReturnAt, row.Amount, row.Currency, $"{row.FirstName} {row.LastName}", row.TermsVersion, row.CreatedAt,
        payments.GetValueOrDefault(row.Id),
        row.CancelledBy is { } by ? new CancellationDto(by.ToString(), row.CancellationReason, row.CancellationAt!.Value, row.Status == Domain.Reservations.ReservationStatus.NoShow) : null,
        reports.Contains(row.Id),
        row.PaymentOption.ToString());

    internal sealed class Row
    {
        public ReservationId Id { get; init; }
        public VehicleCategory Category { get; init; }
        public Domain.Reservations.ReservationStatus Status { get; init; }
        public BranchId PickupBranchId { get; init; }
        public string PickupBranchName { get; init; } = string.Empty;
        public BranchId ReturnBranchId { get; init; }
        public string ReturnBranchName { get; init; } = string.Empty;
        public DateTimeOffset PickupAt { get; init; }
        public DateTimeOffset ReturnAt { get; init; }
        public decimal Amount { get; init; }
        public string Currency { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string TermsVersion { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public Domain.Reservations.CancelledBy? CancelledBy { get; init; }
        public string? CancellationReason { get; init; }
        public DateTimeOffset? CancellationAt { get; init; }
        public Domain.Reservations.PaymentOption PaymentOption { get; init; }
    }
}

/// <summary>
/// Payment status and card summary (brand + last four only) for a set of reservations, in ONE extra
/// AsNoTracking query instead of a join per row. Reservations booked before payments existed have none.
/// </summary>
internal static class PaymentSummaries
{
    public static async Task<IReadOnlyDictionary<ReservationId, PaymentSummaryDto>> ForAsync(
        CarRentalDbContext db, IEnumerable<ReservationId> reservationIds, CancellationToken cancellationToken)
    {
        var ids = reservationIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<ReservationId, PaymentSummaryDto>();

        var rows = await db.Payments.AsNoTracking()
            .Where(p => ids.Contains(p.ReservationId))
            .Select(p => new
            {
                p.ReservationId, p.Status, p.Card.Brand, p.Card.Last4,
                Authorized = p.Authorized.Amount, Captured = p.Captured.Amount, p.Authorized.Currency, p.FailureReason,
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            p => p.ReservationId,
            p => new PaymentSummaryDto(p.Status.ToString(), p.Brand, p.Last4, p.Authorized, p.Captured, p.Currency, p.FailureReason));
    }
}

internal sealed class SqlCustomerReadModel(CarRentalDbContext db) : ICustomerReadModel
{
    public async Task<CustomerProfileDto?> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        var c = await db.Customers.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.Email, x.PhoneNumber, x.Document, x.DateOfBirth, x.LicenceIssuedOn, x.LoyaltyTier })
            .SingleOrDefaultAsync(cancellationToken);

        // The full ID number never leaves this method: only the masked form goes into the DTO.
        return c is null
            ? null
            : new CustomerProfileDto(c.Id.Value, c.FirstName, c.LastName, c.Email, c.PhoneNumber,
                c.Document.Type.ToString(), c.Document.Masked, c.Document.IssuingCountry,
                c.DateOfBirth, c.LicenceIssuedOn, c.LoyaltyTier.ToString());
    }
}

internal sealed class SqlBranchReadModel(CarRentalDbContext db) : IBranchReadModel
{
    public async Task<IReadOnlyList<BranchDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Branches.AsNoTracking()
            .OrderBy(b => b.City).ThenBy(b => b.Type).ThenBy(b => b.Name)
            .Select(b => new { b.Id, b.Code, b.Name, b.City, b.Type, b.Address })
            .ToListAsync(cancellationToken);

        return rows.Select(b => new BranchDto(b.Id.Value, b.Code, b.Name, b.City, b.Type.ToString(), b.Address)).ToList();
    }
}

/// <summary>Row shape for the raw SQL query below (column names must match).</summary>
internal sealed class AvailabilityRow
{
    public string Category { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int Available { get; set; }
}

internal sealed class SqlAvailabilityReadModel(CarRentalDbContext db) : IAvailabilityReadModel
{
    public async Task<IReadOnlyList<CategoryDayAvailability>> GetDailyAvailabilityAsync(
        Guid branchId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        // FormattableString: {branchId}, {from}, {to} are sent as @p0, @p1, @p2 parameters.
        var rows = await db.Database.SqlQuery<AvailabilityRow>($"""
            SELECT i.Category, i.[Date], i.Capacity - i.Reserved AS Available
            FROM rental.CategoryInventory AS i
            WHERE i.BranchId = {branchId} AND i.[Date] BETWEEN {from} AND {to}
            """)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new CategoryDayAvailability(Enum.Parse<VehicleCategory>(r.Category, ignoreCase: true), r.Date, r.Available))
            .ToList();
    }
}
