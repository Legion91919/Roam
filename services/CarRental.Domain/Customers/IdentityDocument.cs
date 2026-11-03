using System.Text.RegularExpressions;
using CarRental.Domain.Common;

namespace CarRental.Domain.Customers;

public enum IdentityDocumentType
{
    SouthAfricanId = 0,
    Passport = 1
}

/// <summary>
/// VALUE OBJECT: the identity document the main driver will show at the counter.
/// Either a South African ID number (validated: date, citizenship digit, checksum) or a passport
/// (alphanumeric number + ISO 3166 alpha-3 issuing country).
/// </summary>
/// <remarks>
/// PRIVACY (POPIA): an ID number is personal information. It is only ever returned masked
/// (<see cref="Masked"/>); production would also encrypt the column at rest.
/// </remarks>
public sealed partial record IdentityDocument
{
    public IdentityDocumentType Type { get; private init; }
    public string Number { get; private init; }
    public string IssuingCountry { get; private init; }

    private IdentityDocument(IdentityDocumentType type, string number, string issuingCountry)
    {
        Type = type;
        Number = number;
        IssuingCountry = issuingCountry;
    }

    public static IdentityDocument SouthAfricanId(string number, DateOnly today)
    {
        var normalised = new string((number ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());
        var problem = SouthAfricanIdNumber.Validate(normalised, today);
        if (problem is not null)
            throw new DomainException("identity.invalid_sa_id", $"The South African ID number {problem}.");

        return new IdentityDocument(IdentityDocumentType.SouthAfricanId, normalised, "ZAF");
    }

    public static IdentityDocument Passport(string number, string issuingCountry)
    {
        var normalised = (number ?? string.Empty).Trim().ToUpperInvariant();
        var country = (issuingCountry ?? string.Empty).Trim().ToUpperInvariant();

        if (!PassportPattern().IsMatch(normalised))
            throw new DomainException("identity.invalid_passport", "Passport number must be 6 to 9 letters or digits.");
        if (!CountryPattern().IsMatch(country))
            throw new DomainException("identity.invalid_country", "Issuing country must be a 3-letter ISO code, e.g. GBR.");

        return new IdentityDocument(IdentityDocumentType.Passport, normalised, country);
    }

    /// <summary>For a South African ID, the date of birth it encodes; null for passports.</summary>
    public DateOnly? ImpliedDateOfBirth(DateOnly today) =>
        Type == IdentityDocumentType.SouthAfricanId ? SouthAfricanIdNumber.DateOfBirth(Number, today) : null;

    /// <summary>Safe to show on screen or in logs: only the last 4 characters.</summary>
    public string Masked => new string('•', Math.Max(0, Number.Length - 4)) + Number[^4..];

    [GeneratedRegex("^[A-Z0-9]{6,9}$")]
    private static partial Regex PassportPattern();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CountryPattern();
}
