using CarRental.Domain.Identifiers;
using CarRental.Domain.ValueObjects;

namespace CarRental.Domain.Pricing;

/// <summary>Everything the pricing rules are allowed to look at.</summary>
public sealed record PricingRequest(
    VehicleCategory Category,
    RentalPeriod Period,
    DriverProfile Driver,
    BranchId PickupBranchId,
    BranchId ReturnBranchId,
    Extras Extras)
{
    /// <summary>Age is judged on the pickup date as the customer sees it (local date of the offset).</summary>
    public DateOnly PickupDate => DateOnly.FromDateTime(Period.PickupAt.Date);
}

/// <summary>One line of the price breakdown shown to the customer ("Weekly discount -R367.50").</summary>
public sealed record PriceLine(string Code, string Description, Money Amount);

/// <summary>The result of pricing: what the customer will pay and why.</summary>
public sealed record PriceQuote(int ChargeableDays, Money DailyRate, Money Total, IReadOnlyList<PriceLine> Lines);
