using CarRental.Application.Abstractions.Terms;

namespace CarRental.Infrastructure.Terms;

/// <summary>
/// ADAPTER for ITermsProvider. The terms live in code for the PoC; a real system would load them
/// from a CMS or a database table so legal can publish a new version without a deployment. Bumping
/// <see cref="Version"/> forces every customer to accept again (TermsAcceptance rejects old versions).
/// Sample terms for a fictional company: not legal advice.
/// </summary>
public sealed class StaticTermsProvider : ITermsProvider
{
    public const string Version = "2026-11-01";

    public TermsDocument Current { get; } = new(
        Version,
        "Roam rental terms and conditions",
        new DateOnly(2026, 11, 1),
        new[]
        {
            new TermsSection("1. Who may drive",
                "The main driver must be at least 21 years old on the pickup date and must have held a full driving licence for at least one year. Luxury vehicles require a driver aged 25 or older. Drivers under 25 pay a young driver fee of R150 per day."),
            new TermsSection("2. Identification",
                "The main driver must present the South African ID or passport registered on their profile, and their original driving licence, at pickup. The name and date of birth must match the booking."),
            new TermsSection("3. Price and charging days",
                "Rental is charged per started 24-hour period, with a 59-minute grace period. Bookings of 7 days or more receive a 15% discount on the base rental. Fees and extras are never discounted. The price shown at booking is the price you pay for the booked period."),
            new TermsSection("4. One-way rentals",
                "Returning the vehicle to a different branch incurs a one-way fee of R750."),
            new TermsSection("5. Late returns",
                "Returns more than 59 minutes after the agreed time are charged at 1.5 times the daily rate for every started 24-hour period of lateness."),
            new TermsSection("6. Cancellation",
                "You may cancel free of charge at any time before the pickup time. After the pickup time a reservation can no longer be cancelled."),
            new TermsSection("7. Vehicle condition and mileage",
                "Return the vehicle in the condition it was collected. The odometer reading is recorded at pickup and at return."),
            new TermsSection("8. Your personal information",
                "We process your identity number and contact details only to manage your rental and to meet legal obligations, in line with the Protection of Personal Information Act (POPIA). Identity numbers are never displayed in full."),
        });
}
