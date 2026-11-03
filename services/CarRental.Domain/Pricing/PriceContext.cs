using CarRental.Domain.ValueObjects;

namespace CarRental.Domain.Pricing;

/// <summary>
/// The running total that flows through the rule pipeline.
/// </summary>
/// <remarks>
/// The price has two buckets, and the split is itself a business rule:
///  * <see cref="Base"/>: the rental charge. Discounts apply here.
///  * <see cref="Fees"/>: surcharges and extras. Discounts NEVER apply here.
/// Rules cannot touch the buckets directly; they call <see cref="ApplyDiscount"/> or
/// <see cref="AddFee"/>, which also record a <see cref="PriceLine"/>. So no rule can, for example,
/// accidentally discount the one-way fee, and every rand of the total is explained.
/// </remarks>
// SOLID (L) support: the context is the ONLY way rules change a price, which is what makes every
//           IPriceRule safely substitutable for any other.
public sealed class PriceContext
{
    private readonly List<PriceLine> _lines = new();

    public PriceContext(PricingRequest request, int chargeableDays, Money dailyRate)
    {
        Request = request;
        ChargeableDays = chargeableDays;
        DailyRate = dailyRate;
        Base = dailyRate * chargeableDays;
        Fees = Money.Zero(dailyRate.Currency);
        _lines.Add(new PriceLine("base", $"{chargeableDays} day(s) x {dailyRate}", Base));
    }

    public PricingRequest Request { get; }
    public int ChargeableDays { get; }
    public Money DailyRate { get; }
    public Money Base { get; private set; }
    public Money Fees { get; private set; }
    public IReadOnlyList<PriceLine> Lines => _lines;

    /// <param name="percent">0.15m means 15% off the CURRENT base (discounts compound in rule order).</param>
    public void ApplyDiscount(string code, string description, decimal percent)
    {
        if (percent is <= 0m or >= 1m) throw new ArgumentOutOfRangeException(nameof(percent));
        var discount = Base * percent;
        Base -= discount;
        _lines.Add(new PriceLine(code, description, discount * -1m));
    }

    public void AddFee(string code, string description, Money amount)
    {
        Fees += amount;
        _lines.Add(new PriceLine(code, description, amount));
    }

    public PriceQuote ToQuote() =>
        new(ChargeableDays, DailyRate, (Base + Fees).Round(), _lines.Select(l => l with { Amount = l.Amount.Round() }).ToList());
}
