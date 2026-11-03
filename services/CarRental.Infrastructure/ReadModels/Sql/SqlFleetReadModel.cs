using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Counter;
using CarRental.Application.Fleet;
using CarRental.Domain.Identifiers;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;
using CarRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.ReadModels.Sql;

/// <summary>READ SIDE for the fleet: AsNoTracking projections, never tracked aggregates.</summary>
internal sealed class SqlFleetReadModel(CarRentalDbContext db) : IFleetReadModel
{
    public async Task<PagedResult<VehicleListItemDto>> ListAsync(VehicleFilter filter, CancellationToken cancellationToken)
    {
        var query = db.Vehicles.AsNoTracking();
        if (filter.BranchId is { } branchId)
        {
            var branch = new BranchId(branchId);
            query = query.Where(v => v.CurrentBranchId == branch);
        }
        if (filter.Category is { } category) query = query.Where(v => v.Category == category);
        if (filter.Status is { } status) query = query.Where(v => v.Status == status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // Parameterised LIKE on the stored text (registration, VIN, make, model): interpolated values are SQL parameters.
            var pattern = $"%{filter.Search.Trim().Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal).Replace("_", "[_]", StringComparison.Ordinal)}%";
            var matches = await db.Database.SqlQuery<Guid>($"""
                SELECT v.Id AS [Value] FROM rental.Vehicles AS v
                WHERE v.Registration LIKE {pattern} OR v.Vin LIKE {pattern} OR v.Make LIKE {pattern} OR v.Model LIKE {pattern}
                """).ToListAsync(cancellationToken);
            var ids = matches.Select(m => new VehicleId(m)).ToList();
            query = query.Where(v => ids.Contains(v.Id));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await (
                from v in query
                join b in db.Branches.AsNoTracking() on v.CurrentBranchId equals b.Id
                orderby b.Code, v.Category, v.Registration
                select new { v.Id, v.Registration, v.Make, v.Model, v.Year, v.Category, v.Transmission, v.Status, v.CurrentBranchId, b.Code, b.Name, v.OdometerKm })
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<VehicleListItemDto>(
            rows.Select(v => new VehicleListItemDto(v.Id.Value, v.Registration.Value, v.Make, v.Model, v.Year, v.Category.ToString(),
                v.Transmission.ToString(), v.Status.ToString(), v.CurrentBranchId.Value, v.Code, v.Name, v.OdometerKm)).ToList(),
            filter.Page, filter.PageSize, total);
    }

    public async Task<VehicleDto?> GetAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var id = new VehicleId(vehicleId);
        var row = await (
                from v in db.Vehicles.AsNoTracking()
                join home in db.Branches.AsNoTracking() on v.HomeBranchId equals home.Id
                join current in db.Branches.AsNoTracking() on v.CurrentBranchId equals current.Id
                where v.Id == id
                select new
                {
                    v.Id, v.Registration, v.Vin, v.Make, v.Model, v.Year, v.Category, v.Transmission, v.Status,
                    v.HomeBranchId, HomeName = home.Name, v.CurrentBranchId, CurrentName = current.Name, v.OdometerKm, v.CreatedAt, v.CurrentRentalId,
                })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;

        Guid? reservationId = null;
        if (row.CurrentRentalId is { } rentalId)
            reservationId = await db.Rentals.AsNoTracking().Where(r => r.Id == rentalId).Select(r => (Guid?)r.ReservationId.Value).SingleOrDefaultAsync(cancellationToken);

        return new VehicleDto(row.Id.Value, row.Registration.Value, row.Vin.Value, row.Make, row.Model, row.Year, row.Category.ToString(),
            row.Transmission.ToString(), row.Status.ToString(), row.HomeBranchId.Value, row.HomeName, row.CurrentBranchId.Value, row.CurrentName,
            row.OdometerKm, row.CreatedAt, row.CurrentRentalId?.Value, reservationId);
    }

    public async Task<VehicleHistoryDto?> GetHistoryAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var vehicle = await GetAsync(vehicleId, cancellationToken);
        if (vehicle is null) return null;
        var id = new VehicleId(vehicleId);

        var log = await db.Vehicles.AsNoTracking().Where(v => v.Id == id).Select(v => v.Log).SingleAsync(cancellationToken);
        var branchNames = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

        var rentals = await (
                from r in db.Rentals.AsNoTracking()
                join res in db.Reservations.AsNoTracking() on r.ReservationId equals res.Id
                join c in db.Customers.AsNoTracking() on res.CustomerId equals c.Id
                where r.VehicleId == id
                select new
                {
                    r.Id, r.ReservationId, c.FirstName, c.LastName, r.PickedUpAt, r.ReturnedAt, r.StartOdometerKm, r.EndOdometerKm,
                    LateFee = r.LateFee.Amount, r.LateFee.Currency, r.ReturnBranchId, res.PickupBranchId,
                })
            .ToListAsync(cancellationToken);
        var inspections = await SqlInspectionReadModel.SummariesAsync(db, rentals.Select(r => r.ReservationId).ToList(), cancellationToken);

        var timeline = new List<VehicleHistoryEventDto>();
        timeline.AddRange(log.Select(entry => new VehicleHistoryEventDto(
            entry.Kind.ToString(), entry.At, entry.OdometerKm, branchNames.GetValueOrDefault(entry.BranchId), entry.Note, null)));
        timeline.AddRange(rentals.Select(r => new VehicleHistoryEventDto(
            "Rental", r.PickedUpAt, r.StartOdometerKm, branchNames.GetValueOrDefault(r.PickupBranchId), null,
            new VehicleRentalHistoryDto(r.Id.Value, r.ReservationId.Value, BookingReference.Format(r.ReservationId.Value), $"{r.FirstName} {r.LastName}",
                r.PickedUpAt, r.ReturnedAt, r.StartOdometerKm, r.EndOdometerKm, r.EndOdometerKm - r.StartOdometerKm, r.LateFee, r.Currency,
                inspections.GetValueOrDefault(r.ReservationId) ?? []))));

        return new VehicleHistoryDto(vehicle, timeline.OrderByDescending(e => e.At).ToList());
    }

    public async Task<IReadOnlyList<AvailableVehicleDto>> ListAvailableAsync(Guid branchId, VehicleCategory category, bool includeUpgrades, CancellationToken cancellationToken)
    {
        var branch = new BranchId(branchId);
        var rows = await db.Vehicles.AsNoTracking()
            .Where(v => v.CurrentBranchId == branch && v.Status == VehicleStatus.Available
                && (v.Category == category || (includeUpgrades && v.Category > category)))
            .OrderBy(v => v.Category).ThenBy(v => v.OdometerKm)
            .Select(v => new { v.Id, v.Registration, v.Make, v.Model, v.Year, v.Category, v.Transmission, v.OdometerKm })
            .ToListAsync(cancellationToken);

        return rows.Select(v => new AvailableVehicleDto(v.Id.Value, v.Registration.Value, v.Make, v.Model, v.Year, v.Category.ToString(),
            v.Transmission.ToString(), v.OdometerKm, v.Category != category)).ToList();
    }
}
