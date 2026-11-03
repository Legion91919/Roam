using System.Globalization;
using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Common.Events;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;
using CarRental.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace CarRental.Infrastructure.ReadModels.Mongo;

// ---------------------------------------------------------------------------------------------
// MONGODB READ MODEL: a denormalised projection of availability, shaped for the search screen.
//
// Why have it at all? Availability search is the hottest, most read-heavy query in a rental site
// (thousands of searches per booking). Serving it from a separate document store lets reads scale
// independently and keeps search traffic off the transactional SQL database. This is the "CQRS"
// in CQRS-lite: same commands, a second store for reads, kept up to date by domain events.
//
// Consistency: EVENTUAL. The projection is updated just after the SQL commit, so a search may be
// a few milliseconds stale. That is fine for SEARCH, because the BOOKING itself is always checked
// against SQL inventory with a concurrency token, so a stale search can never cause an overbooking.
// ---------------------------------------------------------------------------------------------

public sealed class AvailabilityDocument
{
    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>Natural key "{branchId}:{category}:{yyyy-MM-dd}", so updates address one document directly.</summary>
    [BsonId] public string Id { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    /// <summary>ISO date string: sorts and range-filters correctly as text.</summary>
    public string Date { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int Reserved { get; set; }

    public static string MakeId(Guid branchId, VehicleCategory category, DateOnly date) =>
        $"{branchId}:{category}:{date.ToString(DateFormat, CultureInfo.InvariantCulture)}";
}

/// <summary>SOLID (L): a drop-in substitute for SqlAvailabilityReadModel. The query handler cannot tell them apart.</summary>
internal sealed class MongoAvailabilityReadModel(IMongoCollection<AvailabilityDocument> collection) : IAvailabilityReadModel
{
    public async Task<IReadOnlyList<CategoryDayAvailability>> GetDailyAvailabilityAsync(
        Guid branchId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var filter = Builders<AvailabilityDocument>.Filter.Eq(d => d.BranchId, branchId.ToString())
                   & Builders<AvailabilityDocument>.Filter.Gte(d => d.Date, from.ToString(AvailabilityDocument.DateFormat, CultureInfo.InvariantCulture))
                   & Builders<AvailabilityDocument>.Filter.Lte(d => d.Date, to.ToString(AvailabilityDocument.DateFormat, CultureInfo.InvariantCulture));

        var documents = await collection.Find(filter).ToListAsync(cancellationToken);

        return documents
            .Select(d => new CategoryDayAvailability(
                Enum.Parse<VehicleCategory>(d.Category),
                DateOnly.ParseExact(d.Date, AvailabilityDocument.DateFormat, CultureInfo.InvariantCulture),
                d.Capacity - d.Reserved))
            .ToList();
    }
}

/// <summary>
/// EVENT HANDLER that keeps the projection in step with the write model.
/// $inc is atomic per document, so concurrent bookings cannot lose updates.
/// (Idempotency note: with a real message bus and at-least-once delivery, store processed event IDs
/// or project absolute values from SQL instead of increments.)
/// </summary>
internal sealed class MongoAvailabilityProjection(IMongoCollection<AvailabilityDocument> collection) :
    INotificationHandler<DomainEventNotification<ReservationCreated>>,
    INotificationHandler<DomainEventNotification<ReservationCancelled>>
{
    public Task Handle(DomainEventNotification<ReservationCreated> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        return AdjustAsync(e.PickupBranchId, e.Category, e.Period, +1, cancellationToken);
    }

    public Task Handle(DomainEventNotification<ReservationCancelled> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        return AdjustAsync(e.PickupBranchId, e.Category, e.Period, -1, cancellationToken);
    }

    private async Task AdjustAsync(BranchId branchId, VehicleCategory category, RentalPeriod period, int delta, CancellationToken cancellationToken)
    {
        var ids = period.OccupiedDates().Select(date => AvailabilityDocument.MakeId(branchId.Value, category, date)).ToList();

        await collection.UpdateManyAsync(
            Builders<AvailabilityDocument>.Filter.In(d => d.Id, ids),
            Builders<AvailabilityDocument>.Update.Inc(d => d.Reserved, delta),
            cancellationToken: cancellationToken);
    }
}

/// <summary>
/// Projections are DISPOSABLE: they can always be rebuilt from the source of truth (SQL).
/// Run at startup when the Mongo read model is enabled, and any time the projection is suspect.
/// </summary>
public sealed class AvailabilityProjectionRebuilder(CarRentalDbContext db, IMongoCollection<AvailabilityDocument> collection)
{
    public async Task RebuildAsync(CancellationToken cancellationToken)
    {
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<AvailabilityDocument>(
                Builders<AvailabilityDocument>.IndexKeys.Ascending(d => d.BranchId).Ascending(d => d.Date)),
            cancellationToken: cancellationToken);

        var rows = await db.Inventory.AsNoTracking()
            .Select(i => new { i.BranchId, i.Category, i.Date, i.Capacity, i.Reserved })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0) return;

        var writes = rows.Select(r =>
        {
            var document = new AvailabilityDocument
            {
                Id = AvailabilityDocument.MakeId(r.BranchId.Value, r.Category, r.Date),
                BranchId = r.BranchId.Value.ToString(),
                Category = r.Category.ToString(),
                Date = r.Date.ToString(AvailabilityDocument.DateFormat, CultureInfo.InvariantCulture),
                Capacity = r.Capacity,
                Reserved = r.Reserved,
            };
            return new ReplaceOneModel<AvailabilityDocument>(Builders<AvailabilityDocument>.Filter.Eq(d => d.Id, document.Id), document) { IsUpsert = true };
        });

        await collection.BulkWriteAsync(writes, cancellationToken: cancellationToken);
    }
}
