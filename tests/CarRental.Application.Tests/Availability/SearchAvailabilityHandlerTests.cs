using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Availability.SearchAvailability;
using CarRental.Application.Tests.Fakes;
using CarRental.Domain.Pricing;
using CarRental.Domain.ValueObjects;
using static CarRental.Application.Tests.Fakes.TestDomain;

namespace CarRental.Application.Tests.Availability;

public class SearchAvailabilityHandlerTests
{
    private readonly FakeAvailabilityReadModel _readModel = new();

    private void Add(VehicleCategory category, int dayOffset, int available) =>
        _readModel.Rows.Add((Jnb.Value, new CategoryDayAvailability(category, PickupDay.AddDays(dayOffset), available)));

    [Fact]
    public async Task Available_for_the_period_is_the_minimum_across_the_days()
    {
        Add(VehicleCategory.Economy, 0, 5);
        Add(VehicleCategory.Economy, 1, 2);   // busiest day decides
        Add(VehicleCategory.Economy, 2, 4);

        var result = await new SearchAvailabilityHandler(_readModel, RateCard.Standard)
            .Handle(new SearchAvailabilityQuery(Jnb.Value, Pickup, Pickup.AddDays(2)), CancellationToken.None);

        var economy = Assert.Single(result);
        Assert.Equal("Economy", economy.Category);
        Assert.Equal(2, economy.AvailableCars);
        Assert.Equal(350m, economy.DailyRate);
    }

    [Fact]
    public async Task A_category_missing_a_day_is_not_offered()
    {
        Add(VehicleCategory.Suv, 0, 3);
        Add(VehicleCategory.Suv, 2, 3);       // day 1 missing

        var result = await new SearchAvailabilityHandler(_readModel, RateCard.Standard)
            .Handle(new SearchAvailabilityQuery(Jnb.Value, Pickup, Pickup.AddDays(2)), CancellationToken.None);

        Assert.Empty(result);
    }
}
