using CarRental.Domain.Common;

namespace CarRental.Domain.ValueObjects;

public enum LoyaltyTier
{
    None = 0,
    Silver = 1,
    Gold = 2
}

/// <summary>
/// VALUE OBJECT: the facts about the main driver that pricing and eligibility need.
/// Captured at booking time and stored with the reservation, so a later change to the
/// customer's profile cannot silently change an agreed price (a snapshot, on purpose).
/// </summary>
public sealed record DriverProfile
{
    public DateOnly DateOfBirth { get; private init; }
    public DateOnly LicenceIssuedOn { get; private init; }
    public LoyaltyTier Tier { get; private init; }

    public DriverProfile(DateOnly dateOfBirth, DateOnly licenceIssuedOn, LoyaltyTier tier = LoyaltyTier.None)
    {
        if (licenceIssuedOn < dateOfBirth)
            throw new DomainException("driver.licence_before_birth", "Licence issue date cannot be before date of birth.");

        DateOfBirth = dateOfBirth;
        LicenceIssuedOn = licenceIssuedOn;
        Tier = tier;
    }

    /// <summary>Age in whole years on a given date (the birthday must have been reached).</summary>
    public int AgeOn(DateOnly date)
    {
        var age = date.Year - DateOfBirth.Year;
        if (DateOfBirth > date.AddYears(-age)) age--;
        return age;
    }

    public bool HasHeldLicenceForYears(int years, DateOnly on) => LicenceIssuedOn <= on.AddYears(-years);
}
