namespace CarRental.Domain.Pricing;

/// <summary>
/// DOMAIN SERVICE: turns a <see cref="PricingRequest"/> into a <see cref="PriceQuote"/>.
/// </summary>
/// <remarks>
/// It is a domain service (not a method on Reservation) because pricing is needed BEFORE a
/// reservation exists (the quote screen) and does not belong to any single aggregate.
///
/// SOLID in one class:
///  * S: it only orchestrates. Each rule's logic lives in the rule.
///  * O: new rules are added via DI; this class never changes.
///  * D: it depends on the IPriceRule abstraction and an injected RateCard, not on concrete rules.
/// </remarks>
// SOLID (S) Single Responsibility: orchestrates the rules; contains no pricing rule itself.
// SOLID (O) Open/Closed: never edited when a promotion is added; rules arrive through the constructor.
// SOLID (D) Dependency Inversion: depends on IEnumerable<IPriceRule> and RateCard (injected), not on
//           concrete rule classes. The composition root (Api/DI) decides which rules exist.
public sealed class RentalPricer
{
    /// <summary>The first 59 minutes of a new 24-hour block are free.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(59);

    private readonly IReadOnlyList<IPriceRule> _rules;
    private readonly RateCard _rates;

    public RentalPricer(IEnumerable<IPriceRule> rules, RateCard rates)
    {
        _rules = rules.OrderBy(r => r.Order).ToList();
        _rates = rates;
    }

    public PriceQuote Quote(PricingRequest request)
    {
        var days = request.Period.ChargeableDays(GracePeriod);
        var context = new PriceContext(request, days, _rates.DailyRate(request.Category));

        foreach (var rule in _rules)
            rule.Apply(context);

        return context.ToQuote();
    }
}
