using CarRental.Domain.Common;
using CarRental.Domain.Inventory;
using CarRental.Domain.ValueObjects;
using static CarRental.Domain.Tests.TestData;

namespace CarRental.Domain.Tests.Inventory;

/// <summary>
/// The in-memory half of overbooking prevention: one inventory row can never go past its capacity.
/// The concurrent half (two requests at the same instant) is proven against a real SQL Server in
/// CarRental.IntegrationTests/OverbookingTests.
/// </summary>
public class CategoryInventoryTests
{
    private static CategoryInventory Day(DateOnly date, int capacity = 2) =>
        CategoryInventory.Create(JohannesburgAirport, VehicleCategory.Economy, date, capacity);

    [Fact]
    public void Cannot_reserve_more_cars_than_capacity()
    {
        var day = Day(new DateOnly(2026, 12, 1), capacity: 2);
        day.Reserve();
        day.Reserve();

        var ex = Assert.Throws<CapacityExceededException>(day.Reserve);
        Assert.Equal("inventory.sold_out", ex.Code);
        Assert.Equal(2, day.Reserved);
    }

    [Fact]
    public void Releasing_frees_a_car()
    {
        var day = Day(new DateOnly(2026, 12, 1), capacity: 1);
        day.Reserve();
        day.Release();

        Assert.Equal(1, day.Available);
    }

    [Fact]
    public void Capacity_cannot_drop_below_what_is_already_promised()
    {
        var day = Day(new DateOnly(2026, 12, 1), capacity: 3);
        day.Reserve();
        day.Reserve();

        Assert.Throws<DomainException>(() => day.ChangeCapacity(1));
    }

    [Fact]
    public void A_booking_reserves_one_car_on_every_occupied_day()
    {
        var period = Days(2);   // occupies 1, 2 and 3 December
        var days = period.OccupiedDates().Select(d => Day(d)).ToList();

        InventoryAllocation.Reserve(JohannesburgAirport, VehicleCategory.Economy, period, days);

        Assert.All(days, d => Assert.Equal(1, d.Reserved));
    }

    [Fact]
    public void If_any_day_is_sold_out_the_booking_fails()
    {
        var period = Days(2);
        var days = period.OccupiedDates().Select(d => Day(d, capacity: 1)).ToList();
        days[1].Reserve();   // 2 December is already full

        Assert.Throws<CapacityExceededException>(() => InventoryAllocation.Reserve(JohannesburgAirport, VehicleCategory.Economy, period, days));
    }

    [Fact]
    public void If_a_day_has_no_inventory_nothing_is_reserved()
    {
        var period = Days(2);
        var days = period.OccupiedDates().Take(2).Select(d => Day(d)).ToList();   // 3 December missing

        var ex = Assert.Throws<CapacityExceededException>(() => InventoryAllocation.Reserve(JohannesburgAirport, VehicleCategory.Economy, period, days));

        Assert.Equal("inventory.not_offered", ex.Code);
        Assert.All(days, d => Assert.Equal(0, d.Reserved));   // validated before mutating
    }
}
