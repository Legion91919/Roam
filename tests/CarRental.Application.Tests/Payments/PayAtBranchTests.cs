using CarRental.Application.Payments.TakeCounterPayment;
using CarRental.Application.Rentals.CheckOutVehicle;
using CarRental.Application.Rentals.ReturnVehicle;
using CarRental.Application.Reservations.CancelReservation;
using CarRental.Application.Reservations.Cancellation;
using CarRental.Application.Reservations.CreateReservation;
using CarRental.Application.Tests.Fakes;
using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using CarRental.Domain.Payments;
using CarRental.Domain.Pricing;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;
using static CarRental.Application.Tests.Fakes.TestDomain;

namespace CarRental.Application.Tests.Payments;

/// <summary>
/// Pay at the branch: book without a card, the agent takes the card at the counter, then the booking
/// follows the card path (capture at return). Real handlers, in-memory fakes.
/// </summary>
public class PayAtBranchTests
{
    private readonly InMemoryInventoryRepository _inventory = new InMemoryInventoryRepository().Seed(Jnb, VehicleCategory.Economy, PickupDay, 20, 3);
    private readonly InMemoryReservationRepository _reservations = new();
    private readonly InMemoryRentalRepository _rentals = new();
    private readonly InMemoryVehicleRepository _vehicles = new();
    private readonly InMemoryInspectionRepository _inspections = new();
    private readonly InMemoryPaymentRepository _payments = new();
    private readonly FakePaymentGateway _gateway = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new(Now);
    private readonly FakeCurrentUser _agent = new("agent-1", "thandi@roam.test");

    private CreateReservationHandler CreateHandler() => new(new SignedInCustomer(ageOnPickup: 30).Current, new InMemoryBranchRepository(Jnb),
        _inventory, _reservations, _payments, _gateway, _unitOfWork, new FakeTermsProvider(), Pricer(), _clock);
    private TakeCounterPaymentHandler PaymentHandler() => new(_reservations, _payments, _gateway, _unitOfWork, _clock);
    private CheckOutVehicleHandler CheckOutHandler() => new(_reservations, _vehicles, _inspections, _payments, _rentals, _unitOfWork, RateCard.Standard, _clock);
    private ReturnVehicleHandler ReturnHandler() => new(_rentals, _reservations, _vehicles, _inspections, _payments, _gateway, _unitOfWork, _clock);
    private BookingRelease Release() => new(_inventory, _payments, _gateway, new OneDayRateNoShowFee(RateCard.Standard));

    private static CreateReservationCommand Booking(PaymentOption option, string? token = null) => new(
        VehicleCategory.Economy, Jnb.Value, Jnb.Value, Pickup, Pickup.AddDays(3), FakeTermsProvider.Version,
        PaymentToken: token, PaymentOption: option);

    private async Task<Guid> BookToPayAtBranchAsync() =>
        (await CreateHandler().Handle(Booking(PaymentOption.PayAtBranch), CancellationToken.None)).ReservationId;

    private async Task<Guid> CheckOutAsync(Guid reservationId)
    {
        var vehicle = NewVehicle(VehicleCategory.Economy, Jnb, odometerKm: 30_000);
        _vehicles.Add(vehicle);
        _inspections.Add(CompletedCheckout(new ReservationId(reservationId), vehicle.Id, 30_000));
        _clock.Now = Pickup;
        return await CheckOutHandler().Handle(new CheckOutVehicleCommand(reservationId, vehicle.Id.Value), CancellationToken.None);
    }

    [Fact]
    public async Task Booking_to_pay_at_the_branch_takes_nothing_from_a_card()
    {
        var result = await CreateHandler().Handle(Booking(PaymentOption.PayAtBranch), CancellationToken.None);

        Assert.Null(result.Payment);
        Assert.Equal("PayAtBranch", result.PaymentOption);
        Assert.Empty(_gateway.AuthorizeKeys);
        Assert.Empty(_payments.Items);
        Assert.Equal(PaymentOption.PayAtBranch, _reservations.Items.Single().PaymentOption);
        Assert.All(_inventory.Items.Where(i => i.Reserved > 0), i => Assert.Equal(1, i.Reserved));   // the car is still reserved
    }

    [Fact]
    public void A_card_booking_needs_a_token_and_a_pay_at_branch_booking_must_not_send_one()
    {
        var validator = new CreateReservationValidator();

        Assert.False(validator.Validate(Booking(PaymentOption.Card, token: null)).IsValid);
        Assert.False(validator.Validate(Booking(PaymentOption.PayAtBranch, token: ApprovedCard)).IsValid);
        Assert.True(validator.Validate(Booking(PaymentOption.Card, token: ApprovedCard)).IsValid);
        Assert.True(validator.Validate(Booking(PaymentOption.PayAtBranch)).IsValid);
    }

    [Fact]
    public async Task The_keys_are_not_handed_over_before_the_counter_payment()
    {
        var id = await BookToPayAtBranchAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => CheckOutAsync(id));

        Assert.Equal("rental.payment_required", ex.Code);
        Assert.Empty(_rentals.Items);
    }

    [Fact]
    public async Task The_agent_holds_the_booking_total_on_the_card_and_the_return_captures_it()
    {
        var id = await BookToPayAtBranchAsync();
        var total = _reservations.Items.Single().QuotedTotal;

        var summary = await PaymentHandler().Handle(new TakeCounterPaymentCommand(id, ApprovedCard), CancellationToken.None);
        var rentalId = await CheckOutAsync(id);
        var rental = _rentals.Items.Single(r => r.Id.Value == rentalId);
        var checkout = _inspections.Items.Single(i => i.Kind == InspectionKind.Checkout);
        _inspections.Add(CompletedReturn(checkout, rental.Id, 30_400, Pickup.AddDays(3)));
        _clock.Now = Pickup.AddDays(3);
        var returned = await ReturnHandler().Handle(new ReturnVehicleCommand(rentalId), CancellationToken.None);

        Assert.Equal("Authorized", summary.Status);
        Assert.Equal(total.Amount, summary.Authorized);
        Assert.Equal("4242", summary.CardLast4);
        Assert.Equal("Captured", returned.Payment!.Status);
        Assert.Equal(total.Amount, returned.Payment.Captured);
    }

    [Fact]
    public async Task A_declined_card_at_the_counter_saves_nothing_and_another_card_can_be_tried()
    {
        var id = await BookToPayAtBranchAsync();

        await Assert.ThrowsAsync<PaymentDeclinedException>(() => PaymentHandler().Handle(new TakeCounterPaymentCommand(id, DeclinedCard), CancellationToken.None));
        Assert.Empty(_payments.Items);

        var summary = await PaymentHandler().Handle(new TakeCounterPaymentCommand(id, ApprovedCard), CancellationToken.None);

        Assert.Equal("Authorized", summary.Status);
        Assert.Equal(2, _gateway.AuthorizeKeys.Distinct().Count());   // one idempotency key per card tried
        Assert.Single(_payments.Items);
    }

    [Fact]
    public async Task The_counter_payment_is_taken_once()
    {
        var id = await BookToPayAtBranchAsync();
        await PaymentHandler().Handle(new TakeCounterPaymentCommand(id, ApprovedCard), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() => PaymentHandler().Handle(new TakeCounterPaymentCommand(id, "tok_ok_2"), CancellationToken.None));

        Assert.Equal("payment.already_taken", ex.Code);
        Assert.Equal(1, _gateway.Holds);
    }

    [Fact]
    public async Task A_booking_paid_online_is_not_charged_again_at_the_counter()
    {
        var id = (await CreateHandler().Handle(Booking(PaymentOption.Card, ApprovedCard), CancellationToken.None)).ReservationId;

        var ex = await Assert.ThrowsAsync<DomainException>(() => PaymentHandler().Handle(new TakeCounterPaymentCommand(id, ApprovedCard), CancellationToken.None));

        Assert.Equal("payment.paid_online", ex.Code);
        Assert.Equal(1, _gateway.Holds);
    }

    [Fact]
    public async Task Cancelling_or_a_no_show_without_a_payment_releases_the_car_and_charges_nothing()
    {
        var cancelled = await BookToPayAtBranchAsync();
        var noShow = await BookToPayAtBranchAsync();

        await new AgentCancelReservationHandler(_agent, _reservations, Release(), _unitOfWork, _clock)
            .Handle(new AgentCancelReservationCommand(cancelled, "Customer phoned"), CancellationToken.None);
        _clock.Now = Pickup.AddHours(3);
        await new MarkNoShowHandler(_agent, _reservations, Release(), _unitOfWork, _clock)
            .Handle(new MarkNoShowCommand(noShow), CancellationToken.None);

        Assert.All(_inventory.Items, i => Assert.Equal(0, i.Reserved));
        Assert.Equal(0, _gateway.Voids);
        Assert.Empty(_gateway.Captures);
    }
}
