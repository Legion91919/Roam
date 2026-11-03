using CarRental.Domain.Common;
using CarRental.Domain.ValueObjects;
using static CarRental.Domain.Tests.TestData;

namespace CarRental.Domain.Tests.ValueObjects;

public class RentalPeriodTests
{
    [Fact]
    public void Return_must_be_after_pickup()
    {
        var ex = Assert.Throws<DomainException>(() => RentalPeriod.Create(Pickup, Pickup.AddHours(-1)));
        Assert.Equal("period.return_before_pickup", ex.Code);
    }

    [Fact]
    public void A_rental_cannot_exceed_90_days()
    {
        var ex = Assert.Throws<DomainException>(() => RentalPeriod.Create(Pickup, Pickup.AddDays(91)));
        Assert.Equal("period.too_long", ex.Code);
    }

    [Fact]
    public void Occupied_dates_include_pickup_and_return_days()
    {
        // 1 Dec 09:00 SAST (07:00 UTC) to 3 Dec 09:00 SAST
        var period = Days(2);

        Assert.Equal(
            new[] { new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 2), new DateOnly(2026, 12, 3) },
            period.OccupiedDates());
    }

    [Theory]
    [InlineData(0, 2, true)]     // same window
    [InlineData(1, 3, true)]     // starts inside
    [InlineData(-1, 1, true)]    // ends inside
    [InlineData(2, 4, false)]    // starts exactly when the other ends: back-to-back is fine
    [InlineData(-2, 0, false)]
    public void Overlap_uses_the_half_open_interval_test(int startOffsetDays, int endOffsetDays, bool expected)
    {
        var booked = Days(2);
        var other = RentalPeriod.Create(Pickup.AddDays(startOffsetDays), Pickup.AddDays(endOffsetDays));

        Assert.Equal(expected, booked.Overlaps(other));
    }

    [Fact]
    public void Periods_with_the_same_times_are_equal()
    {
        Assert.Equal(Days(3), Days(3));
    }
}
