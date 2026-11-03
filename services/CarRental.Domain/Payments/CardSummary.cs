using CarRental.Domain.Common;

namespace CarRental.Domain.Payments;

/// <summary>
/// VALUE OBJECT: the only card data the Domain ever holds. Brand and last four digits, enough to
/// show "Visa •••• 4242" on a receipt.
/// </summary>
/// <remarks>
/// PCI DSS: a full card number (PAN), expiry date or CVC must never enter the Domain, the database
/// or the logs. The browser sends card details straight to the gateway's hosted fields, which return
/// a single-use token. We only ever see the token and this summary. Storing the last four digits is
/// allowed outside PCI scope.
/// </remarks>
public sealed record CardSummary
{
    public string Brand { get; private init; }
    public string Last4 { get; private init; }

    public CardSummary(string brand, string last4)
    {
        if (string.IsNullOrWhiteSpace(brand) || brand.Length > 20)
            throw new DomainException("payment.invalid_card_summary", "A card brand is required.");
        if (last4 is null || last4.Length != 4 || !last4.All(char.IsAsciiDigit))
            throw new DomainException("payment.invalid_card_summary", "Only the last four digits of a card may be stored.");

        Brand = brand.Trim();
        Last4 = last4;
    }

    public override string ToString() => $"{Brand} •••• {Last4}";
}
