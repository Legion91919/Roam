using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Payments;
using CarRental.Domain.Rentals;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;
using static CarRental.Domain.Tests.TestData;

namespace CarRental.Domain.Tests.Payments;

/// <summary>B46-B48: the customer may book without a card and pay at the branch; the agent takes the card at the counter.</summary>
public class PayAtBranchTests
{
    private static readonly Money EconomyRate = Money.Zar(350m);
    private static readonly CardSummary Visa = new("Visa", "4242");

    private static Reservation Booking(PaymentOption option)
    {
        var request = Request(Days(3));
        return Reservation.Create(CustomerId.New(), VehicleCategory.Economy, request.PickupBranchId, request.ReturnBranchId,
            request.Period, request.Driver, Extras.None, Pricer().Quote(request), AcceptedTerms(), Now, paymentOption: option);
    }

    private static Payment HoldFor(Reservation reservation) =>
        Payment.Authorize(reservation.Id, reservation.QuotedTotal, Visa, approved: true, "auth_counter_1", null, Pickup);

    private static Rental CheckOut(Reservation reservation, Payment? payment)
    {
        var vehicle = NewVehicle();
        return Rental.CheckOut(reservation, vehicle, CompletedCheckoutInspection(reservation.Id, vehicle.Id, vehicle.OdometerKm),
            payment, EconomyRate, freeUpgrade: false, Pickup);
    }

    [Fact]
    public void Paying_by_card_when_booking_is_the_default()
    {
        var request = Request(Days(3));
        var reservation = Reservation.Create(CustomerId.New(), VehicleCategory.Economy, request.PickupBranchId, request.ReturnBranchId,
            request.Period, request.Driver, Extras.None, Pricer().Quote(request), AcceptedTerms(), Now);

        Assert.Equal(PaymentOption.Card, reservation.PaymentOption);
    }

    [Fact]
    public void A_booking_can_be_made_to_pay_at_the_branch()
    {
        var reservation = Booking(PaymentOption.PayAtBranch);

        Assert.Equal(PaymentOption.PayAtBranch, reservation.PaymentOption);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public void An_unknown_payment_option_is_refused()
    {
        var ex = Assert.Throws<DomainException>(() => Booking((PaymentOption)7));

        Assert.Equal("reservation.invalid_payment_option", ex.Code);
    }

    [Fact]
    public void No_keys_for_a_pay_at_branch_booking_until_the_payment_is_taken()
    {
        var reservation = Booking(PaymentOption.PayAtBranch);

        var ex = Assert.Throws<DomainException>(() => CheckOut(reservation, payment: null));

        Assert.Equal("rental.payment_required", ex.Code);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public void A_released_hold_or_another_bookings_hold_does_not_count_as_paid()
    {
        var reservation = Booking(PaymentOption.PayAtBranch);
        var voided = HoldFor(reservation);
        voided.Void(approved: true, null, Pickup);
        var otherBooking = HoldFor(Booking(PaymentOption.PayAtBranch));

        Assert.Equal("rental.payment_required", Assert.Throws<DomainException>(() => CheckOut(reservation, voided)).Code);
        Assert.Equal("rental.payment_required", Assert.Throws<DomainException>(() => CheckOut(reservation, otherBooking)).Code);
    }

    [Fact]
    public void Once_the_agent_has_taken_the_payment_the_keys_can_be_handed_over()
    {
        var reservation = Booking(PaymentOption.PayAtBranch);

        var rental = CheckOut(reservation, HoldFor(reservation));

        Assert.Equal(RentalStatus.Open, rental.Status);
        Assert.Equal(ReservationStatus.CheckedOut, reservation.Status);
    }

    [Fact]
    public void The_agent_can_take_the_payment_for_a_confirmed_pay_at_branch_booking()
    {
        var reservation = Booking(PaymentOption.PayAtBranch);

        reservation.EnsureCanTakeCounterPayment(holdAlreadyTaken: false);   // does not throw
    }

    [Fact]
    public void The_payment_is_taken_only_once()
    {
        var ex = Assert.Throws<DomainException>(() => Booking(PaymentOption.PayAtBranch).EnsureCanTakeCounterPayment(holdAlreadyTaken: true));

        Assert.Equal("payment.already_taken", ex.Code);
    }

    [Fact]
    public void A_booking_paid_online_is_not_charged_again_at_the_counter()
    {
        var ex = Assert.Throws<DomainException>(() => Booking(PaymentOption.Card).EnsureCanTakeCounterPayment(holdAlreadyTaken: false));

        Assert.Equal("payment.paid_online", ex.Code);
    }

    [Fact]
    public void A_cancelled_booking_cannot_be_paid_for()
    {
        var reservation = Booking(PaymentOption.PayAtBranch);
        reservation.Cancel(CancellationRequest.ByCustomer("user-1", Now));

        var ex = Assert.Throws<DomainException>(() => reservation.EnsureCanTakeCounterPayment(holdAlreadyTaken: false));

        Assert.Equal("reservation.not_payable", ex.Code);
    }
}
