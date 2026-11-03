using CarRental.Application.Common.Exceptions;
using CarRental.Application.Reservations.CreateReservation;
using CarRental.Application.Tests.Fakes;
using CarRental.Application.Common.Behaviors;
using CarRental.Domain.Common;
using CarRental.Domain.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;
using static CarRental.Application.Tests.Fakes.TestDomain;

namespace CarRental.Application.Tests.Reservations;

/// <summary>
/// The CreateReservation use case end to end, minus the database.
/// Arrange-Act-Assert, one behaviour per test.
/// </summary>
public class CreateReservationHandlerTests
{
    private readonly InMemoryInventoryRepository _inventory = new();
    private readonly InMemoryReservationRepository _reservations = new();
    private readonly InMemoryPaymentRepository _payments = new();
    private readonly FakePaymentGateway _gateway = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CreateReservationHandler Handler(SignedInCustomer signedIn) =>
        new(signedIn.Current, new InMemoryBranchRepository(Jnb, Cpt), _inventory, _reservations, _payments, _gateway, _unitOfWork,
            new FakeTermsProvider(), Pricer(), new FixedClock(Now));

    private static CreateReservationCommand Command(int days = 3, Guid? returnBranch = null, string? terms = FakeTermsProvider.Version, string card = ApprovedCard) => new(
        Category: VehicleCategory.Economy,
        PickupBranchId: Jnb.Value,
        ReturnBranchId: returnBranch ?? Jnb.Value,
        PickupAt: Pickup,
        ReturnAt: Pickup.AddDays(days),
        AcceptedTermsVersion: terms,
        PaymentToken: card);

    [Fact]
    public async Task Books_for_the_signed_in_customer_reserves_every_day_and_commits_once()
    {
        _inventory.Seed(Jnb, VehicleCategory.Economy, PickupDay, days: 10, capacity: 5);
        var signedIn = new SignedInCustomer();

        var result = await Handler(signedIn).Handle(Command(days: 3), CancellationToken.None);

        var reservation = Assert.Single(_reservations.Items);
        Assert.Equal(result.ReservationId, reservation.Id.Value);
        Assert.Equal(signedIn.Customer.Id, reservation.CustomerId);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal(FakeTermsProvider.Version, reservation.Terms.Version);
        Assert.Equal(1050m, result.Price.Total);
        // 3-day rental 1 Dec 07:00 UTC to 4 Dec 07:00 UTC occupies 1, 2, 3 and 4 Dec
        Assert.Equal(new[] { 1, 1, 1, 1, 0 }, Enumerable.Range(0, 5).Select(i => _inventory.On(PickupDay.AddDays(i)).Reserved));
        Assert.Equal(1, _unitOfWork.Commits);
    }

    [Fact]
    public async Task Loyalty_tier_comes_from_the_profile_not_the_request()
    {
        _inventory.Seed(Jnb, VehicleCategory.Economy, PickupDay, days: 10, capacity: 5);

        var result = await Handler(new SignedInCustomer(tier: LoyaltyTier.Gold)).Handle(Command(days: 1), CancellationToken.None);

        Assert.Equal(315m, result.Price.Total);   // 350 - 10% Gold
    }

    [Fact]
    public async Task Anonymous_users_cannot_book()
    {
        var signedIn = new SignedInCustomer().SwitchTo(null!);

        await Assert.ThrowsAsync<UnauthenticatedException>(() => Handler(signedIn).Handle(Command(), CancellationToken.None));
    }

    [Fact]
    public async Task A_profile_is_required_before_booking()
    {
        var signedIn = new SignedInCustomer().SwitchTo("user-without-profile");

        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler(signedIn).Handle(Command(), CancellationToken.None));
        Assert.Equal("customer.profile_required", ex.Code);
    }

    [Theory]
    [InlineData(null, "terms.not_accepted")]
    [InlineData("2025-01-01", "terms.outdated")]
    public async Task Current_terms_must_be_accepted(string? accepted, string code)
    {
        _inventory.Seed(Jnb, VehicleCategory.Economy, PickupDay, days: 10, capacity: 5);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler(new SignedInCustomer()).Handle(Command(terms: accepted), CancellationToken.None));

        Assert.Equal(code, ex.Code);
        Assert.Empty(_reservations.Items);
    }

    [Fact]
    public async Task The_quote_returned_includes_the_one_way_fee_line()
    {
        _inventory.Seed(Jnb, VehicleCategory.Economy, PickupDay, days: 10, capacity: 5);

        var result = await Handler(new SignedInCustomer()).Handle(Command(days: 1, returnBranch: Cpt.Value), CancellationToken.None);

        Assert.Equal(350m + 750m, result.Price.Total);
        Assert.Contains(result.Price.Lines, l => l.Code == "one_way");
    }

    [Fact]
    public async Task Unknown_return_branch_is_rejected()
    {
        _inventory.Seed(Jnb, VehicleCategory.Economy, PickupDay, days: 10, capacity: 5);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler(new SignedInCustomer()).Handle(Command(returnBranch: Guid.NewGuid()), CancellationToken.None));
        Assert.Equal("branch.not_found", ex.Code);
    }

    [Fact]
    public async Task Sold_out_day_rejects_the_booking_and_nothing_is_saved()
    {
        _inventory.Seed(Jnb, VehicleCategory.Economy, PickupDay, days: 10, capacity: 1);
        _inventory.On(PickupDay.AddDays(1)).Reserve();   // 2 Dec: the only car is taken

        var ex = await Assert.ThrowsAsync<CapacityExceededException>(() => Handler(new SignedInCustomer()).Handle(Command(days: 3), CancellationToken.None));

        Assert.Equal("inventory.sold_out", ex.Code);
        Assert.Empty(_reservations.Items);
        Assert.Equal(0, _unitOfWork.Commits);
    }

    [Fact]
    public async Task Ineligible_driver_is_rejected_before_inventory_is_touched()
    {
        _inventory.Seed(Jnb, VehicleCategory.Economy, PickupDay, days: 10, capacity: 5);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler(new SignedInCustomer(ageOnPickup: 20)).Handle(Command(), CancellationToken.None));

        Assert.Equal("eligibility.under_age", ex.Code);
        Assert.All(_inventory.Items, i => Assert.Equal(0, i.Reserved));
        Assert.Equal(0, _unitOfWork.Commits);
    }

    [Fact]
    public async Task Last_car_can_only_be_booked_once()
    {
        _inventory.Seed(Jnb, VehicleCategory.Economy, PickupDay, days: 10, capacity: 1);
        var signedIn = new SignedInCustomer();

        await Handler(signedIn).Handle(Command(), CancellationToken.None);
        await Assert.ThrowsAsync<CapacityExceededException>(() => Handler(signedIn).Handle(Command(), CancellationToken.None));

        Assert.Single(_reservations.Items);
    }

    [Fact]
    public async Task Booking_authorises_the_quoted_total_and_saves_the_payment_with_the_reservation()
    {
        _inventory.Seed(Jnb, VehicleCategory.Economy, PickupDay, days: 10, capacity: 5);

        var result = await Handler(new SignedInCustomer()).Handle(Command(days: 3), CancellationToken.None);

        var payment = Assert.Single(_payments.Items);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal(Money.Zar(1050m), payment.Authorized);
        Assert.Equal(result.ReservationId, payment.ReservationId.Value);
        Assert.Equal("Visa", payment.Card.Brand);
        Assert.Equal("4242", result.Payment!.CardLast4);
        Assert.Equal(1, _unitOfWork.Commits);   // reservation + payment in one transaction
    }

    [Fact]
    public async Task A_declined_card_stops_the_booking_and_nothing_is_saved()
    {
        _inventory.Seed(Jnb, VehicleCategory.Economy, PickupDay, days: 10, capacity: 5);

        var ex = await Assert.ThrowsAsync<PaymentDeclinedException>(() => Handler(new SignedInCustomer()).Handle(Command(card: DeclinedCard), CancellationToken.None));

        Assert.Equal("payment.declined", ex.Code);
        Assert.Equal("card_declined", ex.Reason);
        Assert.Empty(_reservations.Items);
        Assert.Empty(_payments.Items);
        Assert.Equal(0, _unitOfWork.Commits);
    }

    [Fact]
    public async Task A_retry_after_losing_a_race_does_not_authorise_the_card_twice()
    {
        _inventory.Seed(Jnb, VehicleCategory.Economy, PickupDay, days: 10, capacity: 5);
        _unitOfWork.ConflictsToSimulate = 1;   // the first save loses an inventory race
        var retry = new ConcurrencyRetryBehavior<CreateReservationCommand, CreateReservationResult>(
            _unitOfWork, NullLogger<ConcurrencyRetryBehavior<CreateReservationCommand, CreateReservationResult>>.Instance);
        var handler = Handler(new SignedInCustomer());
        var command = Command();

        var result = await retry.Handle(command, () => handler.Handle(command, CancellationToken.None), CancellationToken.None);

        Assert.Equal(2, _gateway.AuthorizeKeys.Count);            // the handler ran twice...
        Assert.Single(_gateway.AuthorizeKeys.Distinct());          // ...with the same idempotency key...
        Assert.Equal(1, _gateway.Holds);                           // ...so the card was held only once
        Assert.Equal(command.ReservationId, result.ReservationId);
        Assert.Equal(1, _unitOfWork.Commits);
    }
}
