using CarRental.Domain.Common;

namespace CarRental.Domain.ValueObjects;

/// <summary>
/// VALUE OBJECT: an amount in a currency. Immutable, compared by value, no identity.
/// </summary>
/// <remarks>
/// Why a type instead of a decimal?
///  * It makes "add rands to dollars" impossible: mixing currencies throws.
///  * Money is ALWAYS decimal, never double (binary floating point cannot store 0.10 exactly).
///  * Rounding happens in one place (<see cref="Round"/>), once, at the end of a calculation,
///    with an explicit MidpointRounding mode, instead of being scattered through the code.
/// Amounts may be negative so a discount can be shown as a negative price line.
/// EF Core maps it as an OWNED TYPE: two columns (Amount, Currency) on the owner's table.
/// </remarks>
public sealed record Money
{
    public const string DefaultCurrency = "ZAR";

    public decimal Amount { get; private init; }
    public string Currency { get; private init; }

    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new DomainException("money.invalid_currency", "Currency must be a 3-letter ISO 4217 code.");

        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
    }

    public static Money Zar(decimal amount) => new(amount, DefaultCurrency);
    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public bool IsNegative => Amount < 0m;

    /// <summary>Round to cents. Banks and invoices expect AwayFromZero (0.005 becomes 0.01).</summary>
    public Money Round() => this with { Amount = Math.Round(Amount, 2, MidpointRounding.AwayFromZero) };

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left with { Amount = left.Amount + right.Amount };
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left with { Amount = left.Amount - right.Amount };
    }

    public static Money operator *(Money money, decimal factor) => money with { Amount = money.Amount * factor };
    public static Money operator *(Money money, int factor) => money with { Amount = money.Amount * factor };

    public static Money Min(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount ? left : right;
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new DomainException("money.currency_mismatch", $"Cannot combine {left.Currency} with {right.Currency}.");
    }

    public override string ToString() => $"{Currency} {Amount:0.00}";
}
