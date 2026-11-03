using CarRental.Domain.Pricing;
using CarRental.Domain.Pricing.Rules;
using CarRental.Domain.ValueObjects;
using static CarRental.Domain.Tests.TestData;

namespace CarRental.Domain.Tests.Pricing;

/// <summary>
/// TDD: these tests were written from the business rules, before the rules were implemented.
/// Each test name reads as a specification sentence.
/// Rates: Economy R350, Compact R450, SUV R750, Luxury R1500 per day.
/// </summary>
public class RentalPricerTests
{
    private readonly RentalPricer _pricer = Pricer();

    [Theory]
    [InlineData(1, 0, 1)]       // 1 hour: minimum one day
    [InlineData(24, 0, 1)]      // exactly 24h
    [InlineData(24, 59, 1)]     // inside the 59-minute grace
    [InlineData(25, 0, 2)]      // 1 minute past grace starts a new day
    [InlineData(48, 0, 2)]
    [InlineData(48, 60, 3)]
    public void Every_started_24_hours_is_a_day_after_a_59_minute_grace(int hours, int minutes, int expectedDays)
    {
        var quote = _pricer.Quote(Request(PeriodOf(TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes))));

        Assert.Equal(expectedDays, quote.ChargeableDays);
        Assert.Equal(350m * expectedDays, quote.Total.Amount);
    }

    [Theory]
    [InlineData(VehicleCategory.Economy, 350)]
    [InlineData(VehicleCategory.Compact, 450)]
    [InlineData(VehicleCategory.Suv, 750)]
    [InlineData(VehicleCategory.Luxury, 1500)]
    public void Base_price_is_the_category_daily_rate(VehicleCategory category, decimal expected)
    {
        Assert.Equal(expected, _pricer.Quote(Request(category: category)).Total.Amount);
    }

    [Fact]
    public void Six_days_get_no_weekly_discount()
    {
        Assert.Equal(6 * 350m, _pricer.Quote(Request(Days(6))).Total.Amount);
    }

    [Fact]
    public void Seven_days_get_15_percent_weekly_discount()
    {
        // 7 x 350 = 2450, less 15% = 2082.50
        var quote = _pricer.Quote(Request(Days(7)));

        Assert.Equal(2082.50m, quote.Total.Amount);
        Assert.Contains(quote.Lines, l => l.Code == "weekly_discount" && l.Amount.Amount == -367.50m);
    }

    [Theory]
    [InlineData(LoyaltyTier.None, 2082.50)]
    [InlineData(LoyaltyTier.Silver, 1978.38)]   // 2082.50 x 0.95 = 1978.375, rounded away from zero
    [InlineData(LoyaltyTier.Gold, 1874.25)]     // 2082.50 x 0.90
    public void Loyalty_discount_compounds_after_the_weekly_discount(LoyaltyTier tier, decimal expected)
    {
        Assert.Equal(expected, _pricer.Quote(Request(Days(7), driver: DriverAged(35, tier))).Total.Amount);
    }

    [Theory]
    [InlineData(24, 350 + 150)]
    [InlineData(25, 350)]       // turns 25 exactly on the pickup date: no fee
    public void Drivers_under_25_pay_R150_per_day(int age, decimal expected)
    {
        Assert.Equal(expected, _pricer.Quote(Request(driver: DriverAged(age))).Total.Amount);
    }

    [Fact]
    public void Fees_are_never_discounted()
    {
        // 7 days young driver: base 2450 - 15% = 2082.50, plus 7 x 150 = 1050 (NOT discounted) = 3132.50
        Assert.Equal(3132.50m, _pricer.Quote(Request(Days(7), driver: DriverAged(23))).Total.Amount);
    }

    [Fact]
    public void Returning_to_another_branch_adds_a_one_way_fee()
    {
        Assert.Equal(350m + 750m, _pricer.Quote(Request(returnBranch: CapeTownAirport)).Total.Amount);
    }

    [Theory]
    [InlineData(Extras.Gps, 2, 700 + 120)]
    [InlineData(Extras.Gps, 6, 2100 + 300)]          // 6 x 60 = 360, capped at 300
    [InlineData(Extras.ChildSeat, 3, 1050 + 150)]
    [InlineData(Extras.ChildSeat, 6, 2100 + 250)]    // 6 x 50 = 300, capped at 250
    [InlineData(Extras.Gps | Extras.ChildSeat, 1, 350 + 60 + 50)]
    public void Extras_are_charged_per_day_with_a_cap(Extras extras, int days, decimal expected)
    {
        Assert.Equal(expected, _pricer.Quote(Request(Days(days), extras: extras)).Total.Amount);
    }

    [Fact]
    public void Everything_together()
    {
        // 10-day SUV, Gold member aged 24, one-way, GPS + child seat:
        // base 7500 -15% = 6375 -10% = 5737.50; young 10x150 = 1500; one-way 750; GPS 300; seat 250
        var request = Request(Days(10), VehicleCategory.Suv, DriverAged(24, LoyaltyTier.Gold), CapeTownAirport, Extras.Gps | Extras.ChildSeat);

        Assert.Equal(5737.50m + 1500m + 750m + 300m + 250m, _pricer.Quote(request).Total.Amount);
    }

    [Fact]
    public void Rule_order_comes_from_the_rules_not_from_registration_order()
    {
        var reversed = new RentalPricer(AllRules().Reverse(), RateCard.Standard);
        var request = Request(Days(7), driver: DriverAged(23, LoyaltyTier.Gold));

        Assert.Equal(_pricer.Quote(request).Total, reversed.Quote(request).Total);
    }

    [Fact]
    public void Price_lines_explain_the_total()
    {
        var quote = _pricer.Quote(Request(Days(7), driver: DriverAged(23, LoyaltyTier.Silver), returnBranch: CapeTownAirport, extras: Extras.Gps));

        Assert.Equal(quote.Total.Amount, quote.Lines.Sum(l => l.Amount.Amount), precision: 1);
        Assert.Equal(new[] { "base", "weekly_discount", "loyalty_silver", "young_driver", "one_way", "extra_gps" }, quote.Lines.Select(l => l.Code));
    }

    // ------------------------------------------------------------------------------------------
    // SOLID (O) proven by a test: a brand-new rule changes the price WITHOUT editing RentalPricer
    // or any existing rule. This is what "open for extension, closed for modification" buys us.
    // ------------------------------------------------------------------------------------------
    private sealed class BlackFridayRule : IPriceRule
    {
        public int Order => 105;   // after weekly (100), before loyalty (110)
        public void Apply(PriceContext context) => context.ApplyDiscount("black_friday", "Black Friday (20%)", 0.20m);
    }

    [Fact]
    public void A_new_rule_can_be_added_without_modifying_the_pricer()
    {
        var pricer = new RentalPricer(AllRules().Append(new BlackFridayRule()), RateCard.Standard);

        // 2 x 350 = 700, less 20% = 560
        Assert.Equal(560m, pricer.Quote(Request(Days(2))).Total.Amount);
    }

    [Fact]
    public void Rates_are_injected_so_a_different_rate_card_needs_no_code_change()
    {
        var summer = new RateCard(new Dictionary<VehicleCategory, Money> { [VehicleCategory.Economy] = Money.Zar(500m) });
        var pricer = new RentalPricer(AllRules(), summer);

        Assert.Equal(500m, pricer.Quote(Request()).Total.Amount);
    }
}
