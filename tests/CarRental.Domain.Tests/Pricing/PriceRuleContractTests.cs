using CarRental.Domain.Pricing;
using CarRental.Domain.ValueObjects;
using static CarRental.Domain.Tests.TestData;

namespace CarRental.Domain.Tests.Pricing;

/// <summary>
/// SOLID (L) Liskov Substitution, as a test.
/// </summary>
/// <remarks>
/// LSP says any implementation must be usable wherever the abstraction is expected, without the
/// caller needing to know which one it got. For IPriceRule the contract is:
///  1. Never throws for a valid request.
///  2. Never makes the fees bucket smaller (fees are only ever added).
///  3. Never increases the base (the base only ever gets discounts).
///  4. Every change it makes is explained by a price line.
/// The same checks run against EVERY rule. A future rule that breaks the contract fails here,
/// before it can surprise RentalPricer in production.
/// </remarks>
public class PriceRuleContractTests
{
    public static TheoryData<string> RuleNames()
    {
        var data = new TheoryData<string>();
        foreach (var rule in AllRules()) data.Add(rule.GetType().Name);
        return data;
    }

    private static PricingRequest[] Scenarios() => new[]
    {
        Request(),
        Request(Days(7)),
        Request(Days(30), VehicleCategory.Suv, DriverAged(22, LoyaltyTier.Gold), CapeTownAirport, Extras.Gps | Extras.ChildSeat),
        Request(PeriodOf(TimeSpan.FromHours(1)), VehicleCategory.Luxury, DriverAged(60, LoyaltyTier.Silver)),
    };

    [Theory]
    [MemberData(nameof(RuleNames))]
    public void Every_rule_honours_the_IPriceRule_contract(string ruleName)
    {
        var rule = AllRules().Single(r => r.GetType().Name == ruleName);

        foreach (var request in Scenarios())
        {
            var context = new PriceContext(request, request.Period.ChargeableDays(RentalPricer.GracePeriod), RateCard.Standard.DailyRate(request.Category));
            var baseBefore = context.Base.Amount;
            var feesBefore = context.Fees.Amount;
            var linesBefore = context.Lines.Count;

            var exception = Record.Exception(() => rule.Apply(context));

            Assert.Null(exception);
            Assert.True(context.Fees.Amount >= feesBefore, $"{ruleName} reduced the fees.");
            Assert.True(context.Base.Amount <= baseBefore, $"{ruleName} increased the base.");
            var changed = context.Base.Amount != baseBefore || context.Fees.Amount != feesBefore;
            Assert.Equal(changed, context.Lines.Count > linesBefore);
        }
    }
}
