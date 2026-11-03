using System.Globalization;

namespace CarRental.Domain.Customers;

/// <summary>
/// Rules of the South African 13-digit identity number: YYMMDD SSSS C A Z
///   YYMMDD  date of birth
///   SSSS    sequence (0000-4999 female, 5000-9999 male) - not used here
///   C       citizenship: 0 = SA citizen, 1 = permanent resident
///   A       historical digit (usually 8 or 9) - not validated, it varies
///   Z       Luhn check digit over the first 12 digits
/// Pure functions, no state: easy to test with a table of cases.
/// </summary>
public static class SouthAfricanIdNumber
{
    public static bool IsValid(string? number, DateOnly today) => Validate(number, today) is null;

    /// <returns>null when valid, otherwise a short reason (used in the DomainException message).</returns>
    public static string? Validate(string? number, DateOnly today)
    {
        if (number is null || number.Length != 13 || !number.All(char.IsAsciiDigit))
            return "must be exactly 13 digits";
        if (DateOfBirth(number, today) is null)
            return "does not start with a valid date of birth (YYMMDD)";
        if (number[10] is not ('0' or '1'))
            return "has an invalid citizenship digit";
        if (!PassesLuhn(number))
            return "failed the checksum (check for a typing error)";
        return null;
    }

    /// <summary>
    /// The date of birth encoded in the number. YY is ambiguous, so the century is the one that puts
    /// the birthday in the past: in 2026, "03" is 2003 and "90" is 1990.
    /// </summary>
    public static DateOnly? DateOfBirth(string number, DateOnly today)
    {
        if (number.Length < 6 || !int.TryParse(number.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var yy))
            return null;

        var year = 2000 + yy;
        if (year > today.Year) year -= 100;

        return DateOnly.TryParseExact($"{year:0000}{number.Substring(2, 4)}", "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date <= today
            ? date
            : null;
    }

    /// <summary>Luhn mod-10: from the right, double every second digit, subtract 9 if over 9; the sum must end in 0.</summary>
    private static bool PassesLuhn(string number)
    {
        var sum = 0;
        for (var i = 0; i < number.Length; i++)
        {
            var digit = number[number.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
        }
        return sum % 10 == 0;
    }
}
