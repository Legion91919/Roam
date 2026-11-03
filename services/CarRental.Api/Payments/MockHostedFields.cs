using System.Globalization;

namespace CarRental.Api.Payments;

/// <summary>
/// Stand-in for a gateway's HOSTED FIELDS (the card form a gateway serves from its own domain).
/// It turns sandbox test cards into single-use tokens. In production this endpoint does not exist on
/// our API at all: the browser posts the card to the gateway and only the token reaches us.
/// </summary>
/// <remarks>
/// PCI DSS: the card number, expiry and CVC are checked in memory and dropped. They are never
/// logged, persisted, put in an exception message or passed to MediatR (whose pipeline logs requests).
/// </remarks>
public static class MockHostedFields
{
    /// <summary>Sandbox test cards → the tokens MockPaymentGateway understands.</summary>
    public static readonly IReadOnlyDictionary<string, string> TestCards = new Dictionary<string, string>
    {
        ["4242424242424242"] = "tok_visa_approve",
        ["5555555555554444"] = "tok_mc_approve",
        ["4000000000000002"] = "tok_decline",
        ["4000000000009995"] = "tok_insufficient",
        ["4000000000000077"] = "tok_slow",
    };

    public sealed record CardTokenRequest(string? Number, string? Expiry, string? Cvc);

    public sealed record CardToken(string Token, string Brand, string Last4);

    /// <returns>The token, or field errors (keyed by field name, never echoing the card data).</returns>
    public static (CardToken? Token, Dictionary<string, string[]> Errors) Tokenize(CardTokenRequest request, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();
        var number = new string((request.Number ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

        if (number.Length is < 12 or > 19 || !PassesLuhn(number))
            errors["number"] = ["Card number is not valid."];
        else if (!TestCards.ContainsKey(number))
            errors["number"] = ["This sandbox only accepts the listed test cards (e.g. 4242 4242 4242 4242)."];

        if (!TryParseExpiry(request.Expiry, out var year, out var month))
            errors["expiry"] = ["Use MM/YY."];
        else if (new DateOnly(year, month, 1).AddMonths(1) <= DateOnly.FromDateTime(now.UtcDateTime))
            errors["expiry"] = ["This card has expired."];

        var cvc = request.Cvc ?? string.Empty;
        if (cvc.Length is < 3 or > 4 || !cvc.All(char.IsAsciiDigit))
            errors["cvc"] = ["CVC is 3 or 4 digits."];

        return errors.Count > 0
            ? (null, errors)
            : (new CardToken(TestCards[number], BrandOf(number), number[^4..]), errors);
    }

    public static string BrandOf(string number) => number switch
    {
        ['4', ..] => "Visa",
        ['5', '1' or '2' or '3' or '4' or '5', ..] => "Mastercard",
        ['3', '4' or '7', ..] => "Amex",
        _ => "Card",
    };

    /// <summary>Luhn mod-10 over all digits (the last one is the check digit).</summary>
    public static bool PassesLuhn(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 1 && (digit *= 2) > 9) digit -= 9;
            sum += digit;
        }
        return sum % 10 == 0;
    }

    private static bool TryParseExpiry(string? expiry, out int year, out int month)
    {
        year = month = 0;
        var parts = (expiry ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).Split('/');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out month)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out year)
            || month is < 1 or > 12)
            return false;
        if (year < 100) year += 2000;
        return year is >= 2000 and <= 2100;
    }
}
