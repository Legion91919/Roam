using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inventory;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;
using static CarRental.Domain.Tests.TestData;

namespace CarRental.Domain.Tests.Vehicles;

public class RegistrationTests
{
    [Theory]
    [InlineData("CA 123-456", "CA 123-456")]
    [InlineData("  ca   123-456 ", "CA 123-456")]
    [InlineData("ND 45-67 GP", "ND 45-67 GP")]
    [InlineData("bc 12 df gp", "BC 12 DF GP")]
    [InlineData("CY 12345", "CY 12345")]
    [InlineData("FHG 123 MP", "FHG 123 MP")]
    public void South_African_plates_are_accepted_and_normalised(string raw, string expected) =>
        Assert.Equal(expected, Registration.Create(raw).Value);

    [Theory]
    [InlineData("")]
    [InlineData("CA")]
    [InlineData("123 456")]
    [InlineData("AB CD EF GP")]          // no digits at all
    [InlineData("CA 123-456 XX")]        // unknown province suffix
    [InlineData("CA 123-456-789-000")]
    public void Anything_else_is_not_a_registration(string raw) =>
        Assert.Equal("vehicle.invalid_registration", Assert.Throws<DomainException>(() => Registration.Create(raw)).Code);

    [Theory]
    [InlineData("WVWZZZ6RZHY123456")]
    [InlineData("aahzzz6rzjy000001")]
    public void A_VIN_is_17_letters_and_digits(string raw) => Assert.Equal(raw.ToUpperInvariant(), Vin.Create(raw).Value);

    [Theory]
    [InlineData("WVWZZZ6RZHY12345")]     // 16
    [InlineData("WVWZZZ6RZHY1234567")]   // 18
    [InlineData("WVWZZZ6RZIY123456")]    // I
    [InlineData("WVWZZZ6RZOY123456")]    // O
    [InlineData("WVWZZZ6RZQY123456")]    // Q
    public void A_VIN_never_has_I_O_or_Q_and_is_exactly_17_characters(string raw) =>
        Assert.Equal("vehicle.invalid_vin", Assert.Throws<DomainException>(() => Vin.Create(raw)).Code);
}

public class VehicleTests
{
    private static Vehicle Register(int year) => Vehicle.Register(Registration.Create("CA 777-001"), Vin.Create("WVWZZZ6RZHY123456"),
        "Toyota", "Corolla", year, VehicleCategory.Compact, Transmission.Automatic, JohannesburgAirport, 5_000, Now);

    [Fact]
    public void A_registered_car_is_available_at_its_home_branch_and_raises_VehicleRegistered()
    {
        var vehicle = Register(Now.Year);

        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.Equal(JohannesburgAirport, vehicle.CurrentBranchId);
        Assert.IsType<VehicleRegistered>(Assert.Single(vehicle.DomainEvents));
        Assert.Equal(VehicleLogKind.Registered, Assert.Single(vehicle.Log).Kind);
    }

    [Fact]
    public void The_model_year_cannot_be_in_the_future() =>
        Assert.Equal("vehicle.invalid_year", Assert.Throws<DomainException>(() => Register(Now.Year + 1)).Code);

    [Fact]
    public void Rental_cars_are_at_most_10_years_old()
    {
        Register(Now.Year - 10);
        Assert.Equal("vehicle.too_old", Assert.Throws<DomainException>(() => Register(Now.Year - 11)).Code);
    }

    [Fact]
    public void Only_an_available_car_can_go_on_rent()
    {
        var vehicle = NewVehicle();
        vehicle.SendToService("Service at 15 000 km", Now);

        Assert.Equal("vehicle.not_available", Assert.Throws<DomainException>(() => vehicle.AssignToRental(RentalId.New(), 10_000, Now)).Code);
    }

    [Fact]
    public void The_odometer_never_goes_backwards()
    {
        var vehicle = NewVehicle(odometerKm: 10_000);

        Assert.Equal("vehicle.odometer_decreased", Assert.Throws<DomainException>(() => vehicle.AssignToRental(RentalId.New(), 9_999, Now)).Code);
        vehicle.AssignToRental(RentalId.New(), 10_000, Now);
        Assert.Equal("vehicle.odometer_decreased", Assert.Throws<DomainException>(() => vehicle.ReturnFromRental(9_000, JohannesburgAirport, Now)).Code);
    }

    [Fact]
    public void Returning_to_another_branch_moves_the_car_and_raises_VehicleMovedBranch()
    {
        var vehicle = NewVehicle();
        vehicle.AssignToRental(RentalId.New(), 10_000, Now);
        vehicle.ClearDomainEvents();

        vehicle.ReturnFromRental(11_400, CapeTownAirport, Now);

        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.Equal(CapeTownAirport, vehicle.CurrentBranchId);
        Assert.Equal(JohannesburgAirport, vehicle.HomeBranchId);
        var moved = Assert.IsType<VehicleMovedBranch>(Assert.Single(vehicle.DomainEvents));
        Assert.Equal((JohannesburgAirport, CapeTownAirport), (moved.FromBranchId, moved.ToBranchId));
        Assert.Equal(VehicleLogKind.MovedBranch, vehicle.Log[^1].Kind);
    }

    [Fact]
    public void Returning_to_the_same_branch_does_not_move_the_car()
    {
        var vehicle = NewVehicle();
        vehicle.AssignToRental(RentalId.New(), 10_000, Now);
        vehicle.ClearDomainEvents();

        vehicle.ReturnFromRental(10_300, JohannesburgAirport, Now);

        Assert.Empty(vehicle.DomainEvents);
    }

    [Fact]
    public void Only_a_car_on_rent_can_come_back_from_a_rental() =>
        Assert.Equal("vehicle.not_on_rent", Assert.Throws<DomainException>(() => NewVehicle().ReturnFromRental(10_000, JohannesburgAirport, Now)).Code);

    [Fact]
    public void Service_needs_a_reason_and_logs_the_visit()
    {
        var vehicle = NewVehicle();

        Assert.Equal("vehicle.reason_required", Assert.Throws<DomainException>(() => vehicle.SendToService(" ", Now)).Code);
        vehicle.SendToService("Brake pads", Now);
        vehicle.ReturnFromService(10_050, Now.AddDays(1));

        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.Equal(10_050, vehicle.OdometerKm);
        Assert.Equal(new[] { VehicleLogKind.Registered, VehicleLogKind.SentToService, VehicleLogKind.ReturnedFromService }, vehicle.Log.Select(l => l.Kind));
        Assert.Equal("Brake pads", vehicle.Log[1].Note);
    }

    [Fact]
    public void Only_a_car_in_service_can_return_from_service() =>
        Assert.Equal("vehicle.not_in_service", Assert.Throws<DomainException>(() => NewVehicle().ReturnFromService(10_000, Now)).Code);

    [Fact]
    public void A_car_on_rent_cannot_be_serviced_or_retired()
    {
        var vehicle = NewVehicle();
        vehicle.AssignToRental(RentalId.New(), 10_000, Now);

        Assert.Equal("vehicle.on_rent", Assert.Throws<DomainException>(() => vehicle.SendToService("Noise", Now)).Code);
        Assert.Equal("vehicle.on_rent", Assert.Throws<DomainException>(() => vehicle.Retire("Sold", Now)).Code);
    }

    [Fact]
    public void Retiring_needs_a_reason_and_raises_VehicleRetired()
    {
        var vehicle = NewVehicle();
        vehicle.ClearDomainEvents();

        Assert.Equal("vehicle.reason_required", Assert.Throws<DomainException>(() => vehicle.Retire(null, Now)).Code);
        vehicle.Retire("Sold at 120 000 km", Now);

        Assert.Equal(VehicleStatus.Retired, vehicle.Status);
        Assert.Equal("Sold at 120 000 km", Assert.IsType<VehicleRetired>(Assert.Single(vehicle.DomainEvents)).Reason);
    }

    [Fact]
    public void Nothing_leaves_retired()
    {
        var vehicle = NewVehicle();
        vehicle.Retire("Written off", Now);

        Assert.Equal("vehicle.retired", Assert.Throws<DomainException>(() => vehicle.AssignToRental(RentalId.New(), 10_000, Now)).Code);
        Assert.Equal("vehicle.retired", Assert.Throws<DomainException>(() => vehicle.SendToService("x", Now)).Code);
        Assert.Equal("vehicle.retired", Assert.Throws<DomainException>(() => vehicle.ReturnFromService(10_000, Now)).Code);
        Assert.Equal("vehicle.retired", Assert.Throws<DomainException>(() => vehicle.Retire("again", Now)).Code);
    }
}

public class FleetCapacityTests
{
    private static List<CategoryInventory> Days(int capacity, int reservedOnSecondDay = 0)
    {
        var days = Enumerable.Range(0, 3).Select(i => CategoryInventory.Create(JohannesburgAirport, VehicleCategory.Economy, new DateOnly(2026, 12, 1).AddDays(i), capacity)).ToList();
        for (var i = 0; i < reservedOnSecondDay; i++) days[1].Reserve();
        return days;
    }

    [Fact]
    public void A_new_car_adds_one_bookable_car_on_every_future_day()
    {
        var days = Days(capacity: 2);

        FleetCapacity.AddCar(days);

        Assert.All(days, d => Assert.Equal(3, d.Capacity));
    }

    [Fact]
    public void Retiring_a_car_removes_one_on_every_future_day()
    {
        var days = Days(capacity: 3, reservedOnSecondDay: 2);

        FleetCapacity.RemoveCar(days);

        Assert.All(days, d => Assert.Equal(2, d.Capacity));
    }

    [Fact]
    public void Retiring_a_car_fails_when_a_day_is_fully_booked_and_changes_nothing()
    {
        var days = Days(capacity: 2, reservedOnSecondDay: 2);

        var ex = Assert.Throws<DomainException>(() => FleetCapacity.RemoveCar(days));

        Assert.Equal("inventory.capacity_below_reserved", ex.Code);
        Assert.All(days, d => Assert.Equal(2, d.Capacity));
    }

    [Fact]
    public void A_car_leaving_on_a_one_way_return_reduces_capacity_where_possible_and_reports_short_days()
    {
        var days = Days(capacity: 2, reservedOnSecondDay: 2);

        var shortDays = FleetCapacity.RemoveCarWherePossible(days);

        Assert.Equal(new[] { 1, 2, 1 }, days.Select(d => d.Capacity));
        Assert.Equal(new[] { new DateOnly(2026, 12, 2) }, shortDays);
    }
}
