using CarRental.Infrastructure.Payments;
using CarRental.Application.Common.Behaviors;
using CarRental.Application.Common.Customers;
using CarRental.Application.Common.Exceptions;
using CarRental.Application.Reservations.CreateReservation;
using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Pricing;
using CarRental.Domain.Pricing.Rules;
using CarRental.Domain.ValueObjects;
using CarRental.Infrastructure.Persistence;
using CarRental.Infrastructure.Persistence.Repositories;
using CarRental.Infrastructure.Terms;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarRental.IntegrationTests;

/// <summary>
/// THE key business guarantee, proven against real SQL Server: the last car cannot be sold twice,
/// no matter how many requests arrive at the same moment.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class OverbookingTests(SqlServerFixture sql)
{
    private static readonly TimeSpan Sast = TimeSpan.FromHours(2);
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, Sast);
    private static readonly DateTimeOffset Pickup = new(2026, 12, 1, 9, 0, 0, Sast);
    private static readonly DateOnly PickupDay = new(2026, 12, 1);

    [Fact]
    public async Task Rowversion_stops_two_contexts_from_both_taking_the_last_car()
    {
        var branch = await sql.NewBranchWithInventoryAsync(VehicleCategory.Economy, PickupDay, days: 1, capacity: 1);

        // Two requests read the same row at the same time: both see Reserved = 0, Capacity = 1.
        await using var first = sql.CreateContext();
        await using var second = sql.CreateContext();
        var rowSeenByFirst = await first.Inventory.SingleAsync(i => i.BranchId == branch);
        var rowSeenBySecond = await second.Inventory.SingleAsync(i => i.BranchId == branch);

        rowSeenByFirst.Reserve();
        rowSeenBySecond.Reserve();   // in memory, both think they got the car

        await first.SaveChangesAsync();                                                       // wins
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync()); // rowversion changed: loses

        await using var verify = sql.CreateContext();
        Assert.Equal(1, (await verify.Inventory.SingleAsync(i => i.BranchId == branch)).Reserved);
    }

    [Fact]
    public async Task Concurrent_bookings_through_the_real_handler_never_exceed_capacity()
    {
        const int capacity = 2;
        const int customers = 8;
        var branch = await sql.NewBranchWithInventoryAsync(VehicleCategory.Suv, PickupDay, days: 5, capacity: capacity);
        var userId = $"user-{Guid.NewGuid():N}";
        await sql.NewCustomerAsync(userId, Now);

        // 8 customers press "Book" at the same moment, each on its own DbContext (like separate requests or servers).
        var attempts = Enumerable.Range(0, customers).Select(_ => Task.Run(() => BookAsync(branch, userId))).ToList();
        var outcomes = await Task.WhenAll(attempts);

        var successes = outcomes.Count(o => o is null);
        Assert.InRange(successes, 1, capacity);
        Assert.All(outcomes.Where(o => o is not null), o => Assert.True(o is CapacityExceededException or ConcurrencyConflictException, o!.GetType().Name));

        await using var verify = sql.CreateContext();
        var saved = await verify.Reservations.CountAsync(r => r.PickupBranchId == branch);
        var reservedPerDay = await verify.Inventory.Where(i => i.BranchId == branch && i.Date <= PickupDay.AddDays(2)).Select(i => i.Reserved).ToListAsync();

        Assert.Equal(successes, saved);                                // every success is persisted...
        Assert.All(reservedPerDay, reserved => Assert.Equal(saved, reserved));   // ...and inventory agrees exactly
    }

    [Fact]
    public async Task Check_constraint_blocks_overbooking_even_from_raw_sql()
    {
        var branch = await sql.NewBranchWithInventoryAsync(VehicleCategory.Luxury, PickupDay, days: 1, capacity: 1);
        await using var db = sql.CreateContext();

        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlAsync(
            $"UPDATE rental.CategoryInventory SET Reserved = Capacity + 1 WHERE BranchId = {branch.Value}"));
    }

    private static readonly MockPaymentGateway Gateway = new();

    /// <summary>One "HTTP request": its own DbContext, real repositories, real unit of work, real retry behaviour.</summary>
    private async Task<Exception?> BookAsync(BranchId branch, string userId)
    {
        await using var db = sql.CreateContext();
        var unitOfWork = new UnitOfWork(db, new NoOpPublisher(), NullLogger<UnitOfWork>.Instance);
        var handler = new CreateReservationHandler(
            new CurrentCustomer(new FixedUser(userId), new CustomerRepository(db)), new BranchRepository(db),
            new CategoryInventoryRepository(db), new ReservationRepository(db), new PaymentRepository(db), Gateway, unitOfWork, new StaticTermsProvider(),
            new RentalPricer(new IPriceRule[] { new WeeklyDiscountRule(), new YoungDriverFeeRule() }, RateCard.Standard),
            new FixedClock(Now));
        var retry = new ConcurrencyRetryBehavior<CreateReservationCommand, CreateReservationResult>(
            unitOfWork, NullLogger<ConcurrencyRetryBehavior<CreateReservationCommand, CreateReservationResult>>.Instance);

        var command = new CreateReservationCommand(VehicleCategory.Suv, branch.Value, branch.Value,
            Pickup, Pickup.AddDays(2), StaticTermsProvider.Version, PaymentToken: "tok_visa_approve");

        try
        {
            await retry.Handle(command, () => handler.Handle(command, CancellationToken.None), CancellationToken.None);
            return null;
        }
        catch (Exception ex) when (ex is CapacityExceededException or ConcurrencyConflictException)
        {
            return ex;
        }
    }
}
