using CarRental.Domain.Identifiers;
using CarRental.Domain.Pricing;
using CarRental.Domain.Pricing.Rules;
using CarRental.Domain.Rentals;
using CarRental.Domain.Terms;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;
using CarRental.Infrastructure.Persistence;
using CarRental.Infrastructure.ReadModels.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarRental.IntegrationTests;

/// <summary>Owned types, strongly typed IDs, shadow rowversion and both read-side styles, against real SQL.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class PersistenceMappingTests(SqlServerFixture sql)
{
    private static readonly TimeSpan Sast = TimeSpan.FromHours(2);
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, Sast);
    private static readonly DateTimeOffset Pickup = new(2026, 12, 10, 9, 0, 0, Sast);
    private static readonly DateOnly PickupDay = new(2026, 12, 10);

    private async Task<(Reservation Reservation, string UserId)> NewReservationAsync(BranchId branch)
    {
        var userId = $"user-{Guid.NewGuid():N}";
        var customer = await sql.NewCustomerAsync(userId, Now);
        var period = RentalPeriod.Create(Pickup, Pickup.AddDays(3));
        var driver = new DriverProfile(new DateOnly(1990, 5, 10), new DateOnly(2010, 1, 1), LoyaltyTier.Gold);
        var quote = new RentalPricer(new IPriceRule[] { new LoyaltyDiscountRule() }, RateCard.Standard)
            .Quote(new PricingRequest(VehicleCategory.Compact, period, driver, branch, branch, Extras.Gps));
        var terms = TermsAcceptance.Accept("2026-11-01", "2026-11-01", Now);
        return (Reservation.Create(customer.Id, VehicleCategory.Compact, branch, branch, period, driver, Extras.Gps, quote, terms, Now), userId);
    }

    [Fact]
    public async Task Reservation_round_trips_with_value_objects_intact()
    {
        var branch = await sql.NewBranchWithInventoryAsync(VehicleCategory.Compact, PickupDay, 4, 3);
        var (original, _) = await NewReservationAsync(branch);

        await using (var db = sql.CreateContext())
        {
            db.Reservations.Add(original);
            await db.SaveChangesAsync();
        }

        await using var read = sql.CreateContext();
        var loaded = await read.Reservations.SingleAsync(r => r.Id == original.Id);

        Assert.Equal(original.Period, loaded.Period);              // value equality on the owned type
        Assert.Equal(original.Driver, loaded.Driver);
        Assert.Equal(original.QuotedTotal, loaded.QuotedTotal);    // 3 x 450 = 1350, -10% Gold = 1215
        Assert.Equal(1215m, loaded.QuotedTotal.Amount);
        Assert.Equal(Extras.Gps, loaded.Extras);
        Assert.Equal("2026-11-01", loaded.Terms.Version);
        Assert.Empty(loaded.DomainEvents);                         // events are not persisted
        Assert.NotNull(read.Entry(loaded).Property<byte[]>("RowVersion").CurrentValue);   // shadow concurrency token
    }

    [Fact]
    public async Task Rowversion_also_protects_aggregates_from_lost_updates()
    {
        var branch = await sql.NewBranchWithInventoryAsync(VehicleCategory.Compact, PickupDay, 4, 3);
        var (reservation, userId) = await NewReservationAsync(branch);
        await using (var db = sql.CreateContext())
        {
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync();
        }

        // The customer cancels online while an agent cancels the same booking at the counter.
        await using var online = sql.CreateContext();
        await using var counter = sql.CreateContext();
        var a = await online.Reservations.SingleAsync(r => r.Id == reservation.Id);
        var b = await counter.Reservations.SingleAsync(r => r.Id == reservation.Id);

        a.Cancel(CancellationRequest.ByCustomer(userId, Now));
        b.Cancel(CancellationRequest.ByAgent("agent-1", "Customer phoned the branch", Now));

        await online.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => counter.SaveChangesAsync());
    }

    [Fact]
    public async Task Sql_read_models_project_without_loading_aggregates()
    {
        var branch = await sql.NewBranchWithInventoryAsync(VehicleCategory.Compact, PickupDay, 4, 3);
        var (reservation, userId) = await NewReservationAsync(branch);
        await using (var db = sql.CreateContext())
        {
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync();
        }

        await using var read = sql.CreateContext();
        var details = await new SqlReservationReadModel(read).GetForUserAsync(reservation.Id.Value, userId, CancellationToken.None);
        var notMine = await new SqlReservationReadModel(read).GetForUserAsync(reservation.Id.Value, "someone-else", CancellationToken.None);
        var availability = await new SqlAvailabilityReadModel(read).GetDailyAvailabilityAsync(branch.Value, PickupDay, PickupDay.AddDays(3), CancellationToken.None);

        Assert.NotNull(details);
        Assert.Null(notMine);   // ownership is enforced by the query itself
        Assert.Equal("Test Driver", details!.DriverName);
        Assert.Equal("Compact", details.Category);
        Assert.Equal("Test branch", details.PickupBranchName);
        Assert.Equal(1215m, details.Total);
        Assert.Equal(4, availability.Count);
        Assert.All(availability, a => Assert.Equal(3, a.Available));
        Assert.Empty(read.ChangeTracker.Entries());   // nothing was tracked: pure read side
    }
}
