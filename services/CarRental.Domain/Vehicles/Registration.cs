using System.Text.RegularExpressions;
using CarRental.Domain.Common;

namespace CarRental.Domain.Vehicles;

/// <summary>
/// VALUE OBJECT: a South African number plate, normalised (upper case, single spaces, trimmed).
/// </summary>
/// <remarks>
/// SA plates differ per province. Two shapes cover the common formats:
///  * Area code + number, Western Cape / KwaZulu-Natal style: "CA 123-456", "CY 12345", "ND 45-67", "NU 1234".
///  * Letters, digits, letters/digits + province suffix: "BC 12 DF GP", "ND 45-67 GP", "FHG 123 MP".
/// Personalised plates ("ROAM 1 GP") also fit the second shape. The plate is a business identifier
/// (unique across the fleet), so the format is checked here, in one place.
/// </remarks>
public sealed partial record Registration
{
    private static readonly string[] Provinces = ["GP", "MP", "NW", "L", "EC", "NC", "FS", "ZN", "NP", "WP"];

    public string Value { get; private init; }

    private Registration(string value) => Value = value;

    public static Registration Create(string? raw)
    {
        var value = Normalise(raw);
        if (value.Length is < 4 or > 16 || !(AreaCodePlate().IsMatch(value) || ProvincePlate(value)))
            throw new DomainException("vehicle.invalid_registration",
                $"'{raw}' is not a valid South African registration (e.g. CA 123-456 or BC 12 DF GP).");
        return new Registration(value);
    }

    public static string Normalise(string? raw) =>
        WhiteSpace().Replace((raw ?? string.Empty).Trim().ToUpperInvariant(), " ");

    private static bool ProvincePlate(string value)
    {
        var parts = value.Split(' ');
        return parts.Length is 3 or 4
            && Provinces.Contains(parts[^1])
            && parts[..^1].All(p => PlatePart().IsMatch(p))
            && parts[..^1].Any(p => p.Any(char.IsAsciiDigit));
    }

    [GeneratedRegex(@"^[A-Z]{1,3} \d{1,6}(-\d{1,4})?$")]
    private static partial Regex AreaCodePlate();

    [GeneratedRegex(@"^[A-Z0-9]{1,6}(-[A-Z0-9]{1,4})?$")]
    private static partial Regex PlatePart();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    public override string ToString() => Value;
}

/// <summary>
/// VALUE OBJECT: Vehicle Identification Number. 17 characters, letters and digits, never I, O or Q
/// (they look like 1 and 0). The check digit (position 9) is only mandatory in North America, so it
/// is not enforced.
/// </summary>
public sealed partial record Vin
{
    public string Value { get; private init; }

    private Vin(string value) => Value = value;

    public static Vin Create(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToUpperInvariant();
        if (!VinPattern().IsMatch(value))
            throw new DomainException("vehicle.invalid_vin", "A VIN is 17 letters and digits, without I, O or Q.");
        return new Vin(value);
    }

    [GeneratedRegex(@"^[A-HJ-NPR-Z0-9]{17}$")]
    private static partial Regex VinPattern();

    public override string ToString() => Value;
}
