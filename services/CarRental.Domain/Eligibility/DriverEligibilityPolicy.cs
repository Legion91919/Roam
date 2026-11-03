using CarRental.Domain.Common;
using CarRental.Domain.ValueObjects;

namespace CarRental.Domain.Eligibility;

/// <summary>
/// POLICY: who may rent what. Pure function of its inputs (no clock, no database),
/// which makes it trivial to test with a table of cases.
/// </summary>
// SOLID (S) Single Responsibility: eligibility only. Pricing, booking and persistence live elsewhere.
public static class DriverEligibilityPolicy
{
    public const int MinimumAge = 21;
    public const int MinimumLicenceYears = 1;
    public const int MinimumAgeForLuxury = 25;

    public static void EnsureEligible(DriverProfile driver, VehicleCategory category, DateOnly pickupDate)
    {
        var age = driver.AgeOn(pickupDate);

        if (age < MinimumAge)
            throw new DomainException("eligibility.under_age", $"Drivers must be at least {MinimumAge} years old on the pickup date.");

        if (!driver.HasHeldLicenceForYears(MinimumLicenceYears, pickupDate))
            throw new DomainException("eligibility.licence_too_new", $"The driver must have held a licence for at least {MinimumLicenceYears} year(s).");

        if (category == VehicleCategory.Luxury && age < MinimumAgeForLuxury)
            throw new DomainException("eligibility.luxury_under_age", $"Luxury vehicles require a driver aged {MinimumAgeForLuxury} or older.");
    }
}
