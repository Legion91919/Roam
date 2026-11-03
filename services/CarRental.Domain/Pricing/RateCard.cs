using CarRental.Domain.Common;
using CarRental.Domain.ValueObjects;

namespace CarRental.Domain.Pricing;

/// <summary>
/// Daily base rate per category. Injected (not a static lookup) so tests can use their own
/// rates and a future version can load rates from the database per branch or season, without
/// any pricing rule changing (Dependency Inversion).
/// </summary>
// SOLID (D) Dependency Inversion: rules and the pricer receive rates; they never look them up statically.
public sealed class RateCard
{
    private readonly IReadOnlyDictionary<VehicleCategory, Money> _dailyRates;

    public RateCard(IReadOnlyDictionary<VehicleCategory, Money> dailyRates) => _dailyRates = dailyRates;

    public static RateCard Standard { get; } = new(new Dictionary<VehicleCategory, Money>
    {
        [VehicleCategory.Economy] = Money.Zar(350m),
        [VehicleCategory.Compact] = Money.Zar(450m),
        [VehicleCategory.Suv] = Money.Zar(750m),
        [VehicleCategory.Luxury] = Money.Zar(1500m),
    });

    /// <summary>
    /// Returns a COPY of the configured rate. Value objects are immutable, so sharing is normally
    /// harmless, but EF Core tracks owned types as dependents of their owner and refuses to let two
    /// owners share one instance. Copying keeps that persistence detail from ever biting.
    /// </summary>
    public Money DailyRate(VehicleCategory category) =>
        _dailyRates.TryGetValue(category, out var rate)
            ? rate with { }
            : throw new DomainException("pricing.no_rate", $"No daily rate is configured for {category}.");
}
