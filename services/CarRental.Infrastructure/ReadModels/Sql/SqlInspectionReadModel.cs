using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Inspections;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using CarRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.ReadModels.Sql;

/// <summary>
/// READ SIDE for inspection reports. AsNoTracking projections of the columns and JSON collections.
/// Storage keys and user ids never leave this class except in <see cref="AttachmentAccess"/>, which
/// the download use case consumes server-side.
/// </summary>
internal sealed class SqlInspectionReadModel(CarRentalDbContext db) : IInspectionReadModel
{
    public async Task<InspectionDto?> GetAsync(Guid inspectionId, CancellationToken cancellationToken)
    {
        var id = new InspectionId(inspectionId);
        return (await ProjectAsync(db.Inspections.Where(i => i.Id == id), cancellationToken)).SingleOrDefault();
    }

    public async Task<IReadOnlyList<InspectionDto>> ListCompletedForOwnerAsync(Guid reservationId, string userId, CancellationToken cancellationToken)
    {
        var id = new ReservationId(reservationId);
        // Ownership is part of the SQL: someone else's booking simply has no reports.
        var owned =
            from i in db.Inspections
            join r in db.Reservations on i.ReservationId equals r.Id
            join c in db.Customers on r.CustomerId equals c.Id
            where i.ReservationId == id && c.UserId == userId && i.Status == InspectionStatus.Completed
            select i;
        return (await ProjectAsync(owned, cancellationToken)).OrderBy(i => i.Kind == nameof(InspectionKind.Return)).ToList();
    }

    public async Task<IReadOnlyList<InspectionSummaryDto>> ListForReservationAsync(Guid reservationId, CancellationToken cancellationToken) =>
        (await SummariesAsync(db, [new ReservationId(reservationId)], cancellationToken)).GetValueOrDefault(new ReservationId(reservationId)) ?? [];

    public async Task<AttachmentAccess?> GetAttachmentAsync(Guid inspectionId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var id = new InspectionId(inspectionId);
        var row = await (
                from i in db.Inspections.AsNoTracking()
                join r in db.Reservations.AsNoTracking() on i.ReservationId equals r.Id
                join c in db.Customers.AsNoTracking() on r.CustomerId equals c.Id
                where i.Id == id
                select new { i.Attachments, i.Status, c.UserId })
            .SingleOrDefaultAsync(cancellationToken);

        var attachment = row?.Attachments.SingleOrDefault(a => a.AttachmentId.Value == attachmentId);
        return attachment is null
            ? null
            : new AttachmentAccess(attachment.StorageKey, attachment.ContentType, attachment.FileName, row!.Status == InspectionStatus.Completed, row.UserId);
    }

    /// <summary>Short summaries per booking, for lists (vehicle history, counter card, "report available").</summary>
    internal static async Task<Dictionary<ReservationId, IReadOnlyList<InspectionSummaryDto>>> SummariesAsync(
        CarRentalDbContext db, IReadOnlyCollection<ReservationId> reservationIds, CancellationToken cancellationToken)
    {
        if (reservationIds.Count == 0) return new();
        var rows = await db.Inspections.AsNoTracking()
            .Where(i => reservationIds.Contains(i.ReservationId))
            .Select(i => new { i.Id, i.ReservationId, i.Kind, i.Status, i.InspectedAt, i.Checklist, i.Damage, i.NewDamage })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(i => i.ReservationId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<InspectionSummaryDto>)g.OrderBy(i => i.Kind).Select(i => new InspectionSummaryDto(
                    i.Id.Value, i.Kind.ToString(), i.Status.ToString(), i.InspectedAt,
                    i.Checklist.Count(c => c.HasDamage) + i.Damage.Count,
                    i.NewDamage.Select(d => $"{d.Panel}: {d.Description}").ToList())).ToList());
    }

    private async Task<List<InspectionDto>> ProjectAsync(IQueryable<InspectionReport> source, CancellationToken cancellationToken)
    {
        var rows = await (
                from i in source.AsNoTracking()
                join v in db.Vehicles.AsNoTracking() on i.VehicleId equals v.Id
                select new
                {
                    i.Id, i.ReservationId, i.RentalId, i.VehicleId, v.Registration, i.Kind, i.Status, i.InspectedByName, i.StartedAt, i.InspectedAt,
                    i.OdometerKm, i.Fuel, i.Checklist, i.Damage, i.Attachments, i.NewDamage, i.CustomerAcknowledgedAt,
                })
            .ToListAsync(cancellationToken);

        return rows.Select(i => new InspectionDto(
                i.Id.Value, i.ReservationId.Value, i.RentalId?.Value, i.VehicleId.Value, i.Registration.Value,
                i.Kind.ToString(), i.Status.ToString(), i.InspectedByName, i.StartedAt, i.InspectedAt, i.OdometerKm, i.Fuel?.Eighths,
                i.Checklist.Select(c => new ChecklistItemDto(c.Code.ToString(), c.Status.ToString(), c.Note)).ToList(),
                i.Damage.Select(d => new DamageMarkDto(d.Panel, d.Description, d.Severity.ToString())).ToList(),
                i.Attachments.Select(a => new AttachmentDto(a.AttachmentId.Value, a.FileName, a.ContentType, a.SizeBytes, a.Angle.ToString(), a.UploadedAt)).ToList(),
                i.NewDamage.Select(d => new NewDamageDto(d.Item?.ToString(), d.Panel, d.Description, d.Severity?.ToString())).ToList(),
                i.CustomerAcknowledgedAt))
            .ToList();
    }
}
