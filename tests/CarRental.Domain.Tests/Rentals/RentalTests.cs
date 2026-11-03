using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using CarRental.Domain.Rentals;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;
using static CarRental.Domain.Tests.TestData;

namespace CarRental.Domain.Tests.Rentals;

public class RentalTests
{
    private static readonly Money EconomyRate = Money.Zar(350m);

    private static Reservation ConfirmedReservation()
    {
        var request = Request(Days(3));
        return Reservation.Create(CustomerId.New(), VehicleCategory.Economy, request.PickupBranchId, request.ReturnBranchId,
            request.Period, request.Driver, Extras.None, Pricer().Quote(request), AcceptedTerms(), Now);
    }

    private static Rental OpenRental(int startKm = 10_000) => OpenRentalWith(NewVehicle(odometerKm: startKm), startKm);

    private static Rental OpenRentalWith(Vehicle vehicle, int startKm)
    {
        _vehicle = vehicle;
        var reservation = ConfirmedReservation();
        _checkout = CompletedCheckoutInspection(reservation.Id, vehicle.Id, startKm);
        return Rental.CheckOut(reservation, vehicle, _checkout, payment: null, EconomyRate, freeUpgrade: false, Pickup);
    }

    private static InspectionReport _checkout = null!;

    /// <summary>Check-out helper for tests that build their own reservation and car.</summary>
    private static Rental Out(Reservation reservation, Vehicle vehicle, bool freeUpgrade = false, DateTimeOffset? at = null, int? km = null) =>
        Rental.CheckOut(reservation, vehicle, CompletedCheckoutInspection(reservation.Id, vehicle.Id, km ?? vehicle.OdometerKm), payment: null, EconomyRate, freeUpgrade, at ?? Pickup);

    /// <summary>Return helper: a completed return inspection with this odometer.</summary>
    private static Money Back(Rental rental, int km, DateTimeOffset at, Vehicle? vehicle = null) =>
        rental.Return(vehicle ?? Car, CompletedReturnInspection(_checkout, rental.Id, km), at);

    private static Vehicle _vehicle = null!;
    private static Vehicle Car => _vehicle;

    [Fact]
    public void Checkout_marks_the_reservation_as_checked_out()
    {
        var reservation = ConfirmedReservation();

        var vehicle = NewVehicle();

        var rental = Out(reservation, vehicle, false, Pickup, 10_000);

        Assert.Equal(ReservationStatus.CheckedOut, reservation.Status);
        Assert.Equal(vehicle.Registration.Value, rental.VehicleRegistration);
        Assert.Equal(vehicle.Id, rental.VehicleId);
        Assert.Equal(VehicleStatus.OnRent, vehicle.Status);
        Assert.Equal(reservation.Period.ReturnAt, rental.DueBackAt);
    }

    [Fact]
    public void A_cancelled_reservation_cannot_be_checked_out()
    {
        var reservation = ConfirmedReservation();
        reservation.Cancel(CancellationRequest.ByCustomer("user-1", Now));

        Assert.Throws<DomainException>(() => Out(reservation, NewVehicle(), false, Pickup, 10_000));
    }

    [Fact]
    public void Collecting_more_than_2_hours_early_is_rejected()
    {
        var ex = Assert.Throws<DomainException>(() => Out(ConfirmedReservation(), NewVehicle(), false, Pickup.AddHours(-3), 10_000));
        Assert.Equal("rental.too_early", ex.Code);
    }

    [Theory]
    [InlineData(0, 0)]                // on time
    [InlineData(59, 0)]               // inside grace
    [InlineData(60, 525)]             // 1 started day x 350 x 1.5
    [InlineData(24 * 60, 525)]        // exactly 24h late: still 1 day
    [InlineData(24 * 60 + 1, 1050)]   // 24h01 late: 2 started days
    [InlineData(-120, 0)]             // early return: no refund, no fee
    public void Late_fee_is_1_5x_daily_rate_per_started_day_after_59_minutes_grace(int minutesLate, decimal expectedFee)
    {
        var rental = OpenRental();

        var fee = Back(rental, 10_500, rental.DueBackAt.AddMinutes(minutesLate));

        Assert.Equal(expectedFee, fee.Amount);
        Assert.Equal(RentalStatus.Closed, rental.Status);
    }

    [Fact]
    public void Odometer_cannot_go_backwards()
    {
        var rental = OpenRental(startKm: 10_000);

        var ex = Assert.Throws<DomainException>(() => Back(rental, 9_999, rental.DueBackAt));

        Assert.Equal("rental.odometer_decreased", ex.Code);
        Assert.Equal(RentalStatus.Open, rental.Status);   // nothing changed
        Assert.Equal(VehicleStatus.OnRent, Car.Status);
    }

    [Fact]
    public void Same_odometer_reading_is_allowed()
    {
        var rental = OpenRental(startKm: 10_000);
        Back(rental, 10_000, rental.DueBackAt);

        Assert.Equal(0, rental.DistanceKm);
    }

    [Fact]
    public void A_rental_can_only_be_returned_once()
    {
        var rental = OpenRental();
        Back(rental, 10_100, rental.DueBackAt);

        var ex = Assert.Throws<DomainException>(() => Back(rental, 10_200, rental.DueBackAt));
        Assert.Equal("rental.already_returned", ex.Code);
    }

    [Fact]
    public void Returning_raises_an_event_with_distance_and_fee()
    {
        var rental = OpenRental(startKm: 10_000);
        rental.ClearDomainEvents();

        Back(rental, 10_420, rental.DueBackAt.AddHours(2));

        var returned = Assert.IsType<VehicleReturned>(Assert.Single(rental.DomainEvents));
        Assert.Equal(420, returned.DistanceKm);
        Assert.Equal(525m, returned.LateFee.Amount);
    }

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    public void A_rental_is_overdue_once_the_59_minute_grace_has_passed(int minutesLate, bool overdue)
    {
        Assert.Equal(overdue, Rental.IsOverdue(Pickup, Pickup.AddMinutes(minutesLate)));
    }

    [Fact]
    public void The_car_must_be_in_the_booked_category()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Out(ConfirmedReservation(), NewVehicle(VehicleCategory.Suv), false, Pickup, 10_000));
        Assert.Equal("rental.category_mismatch", ex.Code);
    }

    [Fact]
    public void A_free_upgrade_allows_a_higher_category_at_the_booked_rate()
    {
        var rental = Out(ConfirmedReservation(), NewVehicle(VehicleCategory.Suv), true, Pickup, 10_000);

        Assert.Equal(VehicleCategory.Economy, rental.Category);
        Assert.Equal(EconomyRate, rental.DailyRate);
    }

    [Fact]
    public void A_free_upgrade_never_allows_a_lower_category()
    {
        var request = Request(Days(3), VehicleCategory.Suv);
        var suvBooking = Reservation.Create(CustomerId.New(), VehicleCategory.Suv, request.PickupBranchId, request.ReturnBranchId,
            request.Period, request.Driver, Extras.None, Pricer().Quote(request), AcceptedTerms(), Now);

        var ex = Assert.Throws<DomainException>(() => Out(suvBooking, NewVehicle(VehicleCategory.Economy), true, Pickup, 10_000));
        Assert.Equal("rental.category_mismatch", ex.Code);
    }

    [Fact]
    public void An_upgrade_to_luxury_still_needs_a_driver_aged_25()
    {
        var request = Request(Days(3), driver: DriverAged(23));
        var reservation = Reservation.Create(CustomerId.New(), VehicleCategory.Economy, request.PickupBranchId, request.ReturnBranchId,
            request.Period, request.Driver, Extras.None, Pricer().Quote(request), AcceptedTerms(), Now);

        var ex = Assert.Throws<DomainException>(() => Out(reservation, NewVehicle(VehicleCategory.Luxury), true, Pickup, 10_000));
        Assert.Equal("eligibility.luxury_under_age", ex.Code);
    }

    [Fact]
    public void The_car_must_be_at_the_pickup_branch()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Out(ConfirmedReservation(), NewVehicle(branch: CapeTownAirport), false, Pickup, 10_000));
        Assert.Equal("rental.vehicle_wrong_branch", ex.Code);
    }

    [Fact]
    public void The_checkout_odometer_cannot_be_below_the_cars_odometer()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Out(ConfirmedReservation(), NewVehicle(odometerKm: 20_000), false, Pickup, 19_999));
        Assert.Equal("vehicle.odometer_decreased", ex.Code);
    }

    [Fact]
    public void A_car_already_on_rent_cannot_be_checked_out_again()
    {
        var vehicle = NewVehicle();
        Out(ConfirmedReservation(), vehicle, false, Pickup, 10_000);

        var ex = Assert.Throws<DomainException>(() => Out(ConfirmedReservation(), vehicle, false, Pickup, 10_000));
        Assert.Equal("vehicle.not_available", ex.Code);
    }

    [Fact]
    public void Returning_checks_the_car_back_in_with_its_new_odometer()
    {
        var rental = OpenRental(startKm: 10_000);

        Back(rental, 10_640, rental.DueBackAt);

        Assert.Equal(VehicleStatus.Available, Car.Status);
        Assert.Equal(10_640, Car.OdometerKm);
        Assert.Null(Car.CurrentRentalId);
    }

    [Fact]
    public void Only_the_car_that_went_out_can_be_returned_on_a_rental()
    {
        var rental = OpenRental();

        var ex = Assert.Throws<DomainException>(() => Back(rental, 10_500, rental.DueBackAt, NewVehicle()));
        Assert.Equal("rental.wrong_vehicle", ex.Code);
    }

    [Fact]
    public void Keys_only_go_out_after_a_completed_checkout_inspection_of_that_car()
    {
        var reservation = ConfirmedReservation();
        var vehicle = NewVehicle();
        var draft = InspectionReport.Start(reservation.Id, vehicle.Id, null, InspectionKind.Checkout, "agent-1", "Thandi", Now);
        var otherCar = CompletedCheckoutInspection(reservation.Id, NewVehicle().Id, 10_000);

        Assert.Equal("rental.inspection_required", Assert.Throws<DomainException>(() => Rental.CheckOut(reservation, vehicle, null, null, EconomyRate, false, Pickup)).Code);
        Assert.Equal("rental.inspection_required", Assert.Throws<DomainException>(() => Rental.CheckOut(reservation, vehicle, draft, null, EconomyRate, false, Pickup)).Code);
        Assert.Equal("rental.inspection_required", Assert.Throws<DomainException>(() => Rental.CheckOut(reservation, vehicle, otherCar, null, EconomyRate, false, Pickup)).Code);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public void The_start_odometer_is_the_one_recorded_on_the_checkout_inspection()
    {
        var rental = Out(ConfirmedReservation(), NewVehicle(odometerKm: 10_000), km: 10_012);

        Assert.Equal(10_012, rental.StartOdometerKm);
    }

    [Fact]
    public void A_rental_is_only_closed_after_a_completed_return_inspection()
    {
        var rental = OpenRental();

        Assert.Equal("rental.inspection_required", Assert.Throws<DomainException>(() => rental.Return(Car, null, rental.DueBackAt)).Code);
        Assert.Equal("rental.inspection_required", Assert.Throws<DomainException>(() => rental.Return(Car, _checkout, rental.DueBackAt)).Code);
        Assert.Equal(RentalStatus.Open, rental.Status);
    }
}
