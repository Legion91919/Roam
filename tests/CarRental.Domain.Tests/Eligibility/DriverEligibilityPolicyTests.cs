using CarRental.Domain.Common;
using CarRental.Domain.Eligibility;
using CarRental.Domain.ValueObjects;
using static CarRental.Domain.Tests.TestData;

namespace CarRental.Domain.Tests.Eligibility;

public class DriverEligibilityPolicyTests
{
    private static readonly DateOnly PickupDate = DateOnly.FromDateTime(Pickup.Date);

    [Fact]
    public void A_driver_who_turns_21_on_the_pickup_date_is_eligible()
    {
        var exception = Record.Exception(() => DriverEligibilityPolicy.EnsureEligible(DriverAged(21, licenceYears: 2), VehicleCategory.Economy, PickupDate));
        Assert.Null(exception);
    }

    [Fact]
    public void A_driver_one_day_short_of_21_is_rejected()
    {
        var driver = new DriverProfile(PickupDate.AddYears(-21).AddDays(1), PickupDate.AddYears(-2));

        var ex = Assert.Throws<DomainException>(() => DriverEligibilityPolicy.EnsureEligible(driver, VehicleCategory.Economy, PickupDate));
        Assert.Equal("eligibility.under_age", ex.Code);
    }

    [Fact]
    public void A_licence_held_for_less_than_a_year_is_rejected()
    {
        var driver = new DriverProfile(new DateOnly(1990, 1, 1), PickupDate.AddMonths(-11));

        var ex = Assert.Throws<DomainException>(() => DriverEligibilityPolicy.EnsureEligible(driver, VehicleCategory.Economy, PickupDate));
        Assert.Equal("eligibility.licence_too_new", ex.Code);
    }

    [Theory]
    [InlineData(24, false)]
    [InlineData(25, true)]
    public void Luxury_cars_need_a_driver_aged_25_or_older(int age, bool eligible)
    {
        var exception = Record.Exception(() => DriverEligibilityPolicy.EnsureEligible(DriverAged(age), VehicleCategory.Luxury, PickupDate));

        Assert.Equal(eligible, exception is null);
    }
}
