using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using CarRental.Domain.Pricing;
using CarRental.Domain.Pricing.Rules;
using CarRental.Domain.Terms;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;

namespace CarRental.Domain.Tests;

/// <summary>
/// Test data builders. Every test uses a FIXED "now" so results never depend on the real clock
/// (the R in F.I.R.S.T.: Repeatable). Nothing here reads DateTime.Now.
/// </summary>
internal static class TestData
{
    public static readonly TimeSpan Sast = TimeSpan.FromHours(2);
    public static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, Sast);
    public static readonly DateTimeOffset Pickup = new(2026, 12, 1, 9, 0, 0, Sast);

    public static readonly BranchId JohannesburgAirport = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    public static readonly BranchId CapeTownAirport = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    /// <summary>A driver who is exactly <paramref name="age"/> on the pickup date, licensed for 5 years.</summary>
    public static DriverProfile DriverAged(int age, LoyaltyTier tier = LoyaltyTier.None, int licenceYears = 5)
    {
        var pickupDate = DateOnly.FromDateTime(Pickup.Date);
        return new DriverProfile(pickupDate.AddYears(-age), pickupDate.AddYears(-Math.Min(licenceYears, age - 18)), tier);
    }

    public static RentalPeriod PeriodOf(TimeSpan length) => RentalPeriod.Create(Pickup, Pickup + length);
    public static RentalPeriod Days(int days) => PeriodOf(TimeSpan.FromDays(days));

    public const string TermsVersion = "2026-11-01";

    public static TermsAcceptance AcceptedTerms() => TermsAcceptance.Accept(TermsVersion, TermsVersion, Now);

    public static IPriceRule[] AllRules() =>
        new IPriceRule[] { new WeeklyDiscountRule(), new LoyaltyDiscountRule(), new YoungDriverFeeRule(), new OneWayFeeRule(), new ExtrasRule() };

    public static RentalPricer Pricer() => new(AllRules(), RateCard.Standard);

    public static PricingRequest Request(
        RentalPeriod? period = null,
        VehicleCategory category = VehicleCategory.Economy,
        DriverProfile? driver = null,
        BranchId? returnBranch = null,
        Extras extras = Extras.None) =>
        new(category, period ?? Days(1), driver ?? DriverAged(35), JohannesburgAirport, returnBranch ?? JohannesburgAirport, extras);

    private static int _plate = 100;

    /// <summary>An Available car at the JNB test branch (unique plate and VIN per call).</summary>
    public static Vehicle NewVehicle(VehicleCategory category = VehicleCategory.Economy, BranchId? branch = null, int odometerKm = 10_000)
    {
        var n = Interlocked.Increment(ref _plate);
        return Vehicle.Register(Registration.Create($"CA {n:000}-{n % 1000:000}"), Vin.Create($"AAVZZZ6RZ{n:00000000}"),
            "Volkswagen", "Polo Vivo", Now.Year - 1, category, Transmission.Manual, branch ?? JohannesburgAirport, odometerKm, Now);
    }

    public static List<ChecklistItem> AllOkChecklist() =>
        Enum.GetValues<ChecklistItemCode>().Select(c => new ChecklistItem(c, ChecklistItemStatus.Ok, null)).ToList();

    /// <summary>A completed Checkout inspection of <paramref name="vehicle"/> for <paramref name="reservationId"/>.</summary>
    public static InspectionReport CompletedCheckoutInspection(ReservationId reservationId, VehicleId vehicleId, int odometerKm)
    {
        var report = InspectionReport.Start(reservationId, vehicleId, null, InspectionKind.Checkout, "agent-1", "Thandi", Now);
        report.Update(odometerKm, FuelLevel.Full, AllOkChecklist(), []);
        foreach (var angle in InspectionReport.RequiredCheckoutAngles)
            report.AddAttachment(new InspectionAttachment(AttachmentId.New(), $"{angle}.jpg", "image/jpeg", 1000, $"k{Guid.NewGuid():N}", angle, Now, "agent-1"));
        report.Complete(Now);
        return report;
    }

    public static InspectionReport CompletedReturnInspection(InspectionReport checkout, RentalId rentalId, int odometerKm)
    {
        var report = InspectionReport.Start(checkout.ReservationId, checkout.VehicleId, rentalId, InspectionKind.Return, "agent-1", "Thandi", Now);
        report.Update(odometerKm, FuelLevel.Full, AllOkChecklist(), []);
        report.Complete(Now, checkout);
        return report;
    }
}
