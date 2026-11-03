using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Counter;
using CarRental.Application.Payments;
using CarRental.Application.Reservations.GetReservationById;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Rentals;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;
using CarRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.ReadModels.Sql;

/// <summary>
/// READ SIDE for the counter (staff). AsNoTracking projections, never aggregates. Unlike the customer
/// read model there is no owner filter: the API only lets the Agent role reach these queries.
/// Three small queries per call (bookings, then their rentals and payments by id) keep the SQL simple
/// and avoid left joins through owned types.
/// </summary>
internal sealed class SqlCounterReadModel(CarRentalDbContext db) : ICounterReadModel
{
    public Task<IReadOnlyList<CounterBookingDto>> ListPickupsAsync(Guid branchId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var branch = new BranchId(branchId);
        return LoadAsync(
            db.Reservations.Where(r => r.PickupBranchId == branch
                && r.Period.PickupAt >= from && r.Period.PickupAt < to
                && (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedOut)),
            rows => rows.OrderBy(r => r.PickupAt),
            cancellationToken);
    }

    public Task<IReadOnlyList<CounterBookingDto>> ListOpenReturnsAsync(Guid branchId, DateTimeOffset dueBefore, CancellationToken cancellationToken)
    {
        var branch = new BranchId(branchId);
        return LoadAsync(
            db.Reservations.Where(r => r.ReturnBranchId == branch
                && db.Rentals.Any(x => x.ReservationId == r.Id && x.Status == RentalStatus.Open && x.DueBackAt < dueBefore)),
            rows => rows.OrderBy(r => r.ReturnAt),
            cancellationToken);
    }

    public async Task<CounterBookingDto?> FindAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var id = new ReservationId(reservationId);
        return (await LoadAsync(db.Reservations.Where(r => r.Id == id), rows => rows, cancellationToken)).SingleOrDefault();
    }

    public async Task<CounterBookingDto?> FindByReferencePrefixAsync(string hexPrefix, CancellationToken cancellationToken)
    {
        // "8F4B92A10C7E449D" -> GUID text starts with "8F4B92A1-0C7E-449D". Parameterised LIKE, no injection.
        var pattern = $"{hexPrefix[..8]}-{hexPrefix[8..12]}-{hexPrefix[12..16]}%";
        var ids = await db.Database
            .SqlQuery<Guid>($"SELECT TOP (2) r.Id AS [Value] FROM rental.Reservations AS r WHERE UPPER(CONVERT(char(36), r.Id)) LIKE {pattern}")
            .ToListAsync(cancellationToken);

        return ids.Count == 1 ? await FindAsync(ids[0], cancellationToken) : null;
    }

    private async Task<IReadOnlyList<CounterBookingDto>> LoadAsync(
        IQueryable<Reservation> reservations, Func<IQueryable<Row>, IQueryable<Row>> order, CancellationToken cancellationToken)
    {
        var query =
            from r in reservations.AsNoTracking()
            join customer in db.Customers.AsNoTracking() on r.CustomerId equals customer.Id
            join pickup in db.Branches.AsNoTracking() on r.PickupBranchId equals pickup.Id
            join dropOff in db.Branches.AsNoTracking() on r.ReturnBranchId equals dropOff.Id
            select new Row
            {
                Id = r.Id,
                Category = r.Category,
                Status = r.Status,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                PickupBranchId = r.PickupBranchId,
                PickupBranchName = pickup.Name,
                ReturnBranchId = r.ReturnBranchId,
                ReturnBranchName = dropOff.Name,
                PickupAt = r.Period.PickupAt,
                ReturnAt = r.Period.ReturnAt,
                Amount = r.QuotedTotal.Amount,
                Currency = r.QuotedTotal.Currency,
                CancelledBy = r.Cancellation == null ? null : r.Cancellation.By,
                CancellationReason = r.Cancellation == null ? null : r.Cancellation.Reason,
                CancellationAt = r.Cancellation == null ? null : r.Cancellation.At,
                PaymentOption = r.PaymentOption,
            };

        var rows = await order(query).Take(200).ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Id).ToList();

        var payments = await PaymentSummaries.ForAsync(db, ids, cancellationToken);
        var rentals = (await db.Rentals.AsNoTracking()
                .Where(x => ids.Contains(x.ReservationId))
                .Select(x => new { x.ReservationId, x.Id, x.VehicleId, x.Status, x.VehicleRegistration, x.StartOdometerKm, x.PickedUpAt, x.DueBackAt })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.ReservationId);
        var inspections = await SqlInspectionReadModel.SummariesAsync(db, ids, cancellationToken);

        return rows.Select(r => new CounterBookingDto(
                r.Id.Value, BookingReference.Format(r.Id.Value), r.Category.ToString(), r.Status.ToString(),
                $"{r.FirstName} {r.LastName}", r.Email,
                r.PickupBranchId.Value, r.PickupBranchName, r.ReturnBranchId.Value, r.ReturnBranchName,
                r.PickupAt, r.ReturnAt, r.Amount, r.Currency,
                payments.GetValueOrDefault(r.Id),
                rentals.TryGetValue(r.Id, out var x)
                    ? new CounterRentalDto(x.Id.Value, x.Status.ToString(), x.VehicleRegistration, x.StartOdometerKm, x.PickedUpAt, x.DueBackAt, IsLate: false)
                    : null,
                r.CancelledBy is { } by ? new CancellationDto(by.ToString(), r.CancellationReason, r.CancellationAt!.Value, r.Status == ReservationStatus.NoShow) : null,
                inspections.GetValueOrDefault(r.Id) ?? [],
                rentals.TryGetValue(r.Id, out var rental) ? rental.VehicleId.Value : null,
                r.PaymentOption.ToString()))
            .ToList();
    }

    internal sealed class Row
    {
        public ReservationId Id { get; init; }
        public VehicleCategory Category { get; init; }
        public ReservationStatus Status { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public BranchId PickupBranchId { get; init; }
        public string PickupBranchName { get; init; } = string.Empty;
        public BranchId ReturnBranchId { get; init; }
        public string ReturnBranchName { get; init; } = string.Empty;
        public DateTimeOffset PickupAt { get; init; }
        public DateTimeOffset ReturnAt { get; init; }
        public decimal Amount { get; init; }
        public string Currency { get; init; } = string.Empty;
        public CancelledBy? CancelledBy { get; init; }
        public string? CancellationReason { get; init; }
        public DateTimeOffset? CancellationAt { get; init; }
        public PaymentOption PaymentOption { get; init; }
    }
}
