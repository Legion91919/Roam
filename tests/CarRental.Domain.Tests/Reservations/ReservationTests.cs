using CarRental.Domain.Pricing;
using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;
using static CarRental.Domain.Tests.TestData;

namespace CarRental.Domain.Tests.Reservations;

public class ReservationTests
{
    private static Reservation NewReservation(RentalPeriod? period = null, DriverProfile? driver = null, VehicleCategory category = VehicleCategory.Economy)
    {
        var request = Request(period ?? Days(3), category, driver);
        return Reservation.Create(CustomerId.New(), category, request.PickupBranchId, request.ReturnBranchId,
            request.Period, request.Driver, Extras.None, Pricer().Quote(request), AcceptedTerms(), Now);
    }

    [Fact]
    public void A_new_reservation_is_confirmed_with_the_quoted_price_and_raises_an_event()
    {
        var reservation = NewReservation();

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal(1050m, reservation.QuotedTotal.Amount);
        var created = Assert.IsType<ReservationCreated>(Assert.Single(reservation.DomainEvents));
        Assert.Equal(reservation.Id, created.ReservationId);
    }

    [Fact]
    public void The_accepted_terms_version_is_stored_with_the_booking()
    {
        Assert.Equal(TermsVersion, NewReservation().Terms.Version);
    }

    [Fact]
    public void Pickup_cannot_be_in_the_past()
    {
        var past = RentalPeriod.Create(Now.AddHours(-2), Now.AddDays(1));

        var ex = Assert.Throws<DomainException>(() => NewReservation(past));
        Assert.Equal("reservation.pickup_in_past", ex.Code);
    }

    [Fact]
    public void Ineligible_drivers_cannot_book()
    {
        var ex = Assert.Throws<DomainException>(() => NewReservation(driver: DriverAged(20)));
        Assert.Equal("eligibility.under_age", ex.Code);
    }

    [Fact]
    public void Cancelling_before_pickup_raises_an_event()
    {
        var reservation = NewReservation();
        reservation.ClearDomainEvents();

        reservation.Cancel(CancellationRequest.ByCustomer("user-1", Now));

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        var cancelled = Assert.IsType<ReservationCancelled>(Assert.Single(reservation.DomainEvents));
        Assert.Equal(CancelledBy.Customer, cancelled.By);
        Assert.Equal(new CancellationDetails(CancelledBy.Customer, "user-1", null, Now), reservation.Cancellation);
    }

    [Fact]
    public void Cannot_cancel_after_pickup_time()
    {
        var reservation = NewReservation();

        var ex = Assert.Throws<DomainException>(() => reservation.Cancel(CancellationRequest.ByCustomer("user-1", Pickup.AddMinutes(1))));
        Assert.Equal("reservation.too_late_to_cancel", ex.Code);
    }

    [Fact]
    public void Cannot_cancel_twice()
    {
        var reservation = NewReservation();
        reservation.Cancel(CancellationRequest.ByCustomer("user-1", Now));

        Assert.Throws<DomainException>(() => reservation.Cancel(CancellationRequest.ByCustomer("user-1", Now)));
    }

    [Fact]
    public void An_agent_must_give_a_reason_to_cancel()
    {
        var reservation = NewReservation();

        var ex = Assert.Throws<DomainException>(() => reservation.Cancel(CancellationRequest.ByAgent("agent-1", "  ", Now)));

        Assert.Equal("reservation.cancel_reason_required", ex.Code);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public void An_agent_may_cancel_after_the_pickup_time_with_a_reason_and_it_is_recorded()
    {
        var reservation = NewReservation();
        reservation.ClearDomainEvents();

        reservation.Cancel(CancellationRequest.ByAgent("agent-1", "Customer called: flight cancelled", Pickup.AddHours(1)));

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(new CancellationDetails(CancelledBy.Agent, "agent-1", "Customer called: flight cancelled", Pickup.AddHours(1)), reservation.Cancellation);
        var cancelled = Assert.IsType<ReservationCancelled>(Assert.Single(reservation.DomainEvents));
        Assert.Equal(("Customer called: flight cancelled", false), (cancelled.Reason, cancelled.NoShow));
    }

    [Fact]
    public void Nobody_can_cancel_after_checkout()
    {
        var reservation = NewReservation();
        var vehicle = NewVehicle();
        CarRental.Domain.Rentals.Rental.CheckOut(reservation, vehicle, CompletedCheckoutInspection(reservation.Id, vehicle.Id, 10_000), null, Money.Zar(350m), false, Pickup);

        var ex = Assert.Throws<DomainException>(() => reservation.Cancel(CancellationRequest.ByAgent("agent-1", "Mistake", Pickup)));
        Assert.Equal("reservation.not_cancellable", ex.Code);
    }

    [Theory]
    [InlineData(119, false)]
    [InlineData(120, true)]
    public void A_no_show_can_be_recorded_from_2_hours_after_pickup(int minutesAfterPickup, bool allowed)
    {
        var reservation = NewReservation();
        var at = Pickup.AddMinutes(minutesAfterPickup);

        if (allowed)
        {
            reservation.MarkNoShow("agent-1", at);
            Assert.Equal(ReservationStatus.NoShow, reservation.Status);
            Assert.Equal(new CancellationDetails(CancelledBy.Agent, "agent-1", "No-show", at), reservation.Cancellation);
            Assert.Contains(reservation.DomainEvents, e => e is ReservationCancelled { NoShow: true });
        }
        else
        {
            Assert.Equal("reservation.no_show_too_early", Assert.Throws<DomainException>(() => reservation.MarkNoShow("agent-1", at)).Code);
        }
    }

    [Fact]
    public void A_cancelled_reservation_cannot_become_a_no_show()
    {
        var reservation = NewReservation();
        reservation.Cancel(CancellationRequest.ByCustomer("user-1", Now));

        Assert.Equal("reservation.not_cancellable", Assert.Throws<DomainException>(() => reservation.MarkNoShow("agent-1", Pickup.AddHours(3))).Code);
    }

    [Fact]
    public void The_no_show_fee_is_one_day_at_the_booked_rate()
    {
        var fee = new OneDayRateNoShowFee(RateCard.Standard).FeeFor(NewReservation());

        Assert.Equal(Money.Zar(350m), fee);
    }

    [Fact]
    public void The_no_show_fee_never_exceeds_the_booking_total()
    {
        var cheapRates = new RateCard(new Dictionary<VehicleCategory, Money> { [VehicleCategory.Economy] = Money.Zar(10_000m) });

        var fee = new OneDayRateNoShowFee(cheapRates).FeeFor(NewReservation());

        Assert.Equal(NewReservation().QuotedTotal, fee);
    }
}
