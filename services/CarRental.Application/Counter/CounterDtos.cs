using System.Globalization;
using CarRental.Application.Payments;
using CarRental.Domain.Rentals;

namespace CarRental.Application.Counter;

// ---------------------------------------------------------------------------------------------
// The counter (staff) view of bookings. Agents see any customer's booking at the counter, so these
// DTOs carry the customer's name and email, but still never an ID number or card data.
// ---------------------------------------------------------------------------------------------

public sealed record CounterBookingDto(
    Guid ReservationId,
    string Reference,
    string Category,
    string Status,
    string CustomerName,
    string CustomerEmail,
    Guid PickupBranchId,
    string PickupBranchName,
    Guid ReturnBranchId,
    string ReturnBranchName,
    DateTimeOffset PickupAt,
    DateTimeOffset ReturnAt,
    decimal Total,
    string Currency,
    PaymentSummaryDto? Payment,
    CounterRentalDto? Rental,
    Reservations.GetReservationById.CancellationDto? Cancellation = null,
    IReadOnlyList<Inspections.InspectionSummaryDto>? Inspections = null,
    Guid? VehicleId = null,
    string PaymentOption = "Card")
{
    /// <summary>Sets <see cref="CounterRentalDto.IsLate"/> from the Domain's overdue rule (due time + 59 min grace).</summary>
    public CounterBookingDto WithLateFlag(DateTimeOffset now) =>
        Rental is { Status: nameof(RentalStatus.Open) } rental
            ? this with { Rental = rental with { IsLate = Domain.Rentals.Rental.IsOverdue(rental.DueBackAt, now) } }
            : this;
}

public sealed record CounterRentalDto(
    Guid RentalId,
    string Status,
    string VehicleRegistration,
    int StartOdometerKm,
    DateTimeOffset PickedUpAt,
    DateTimeOffset DueBackAt,
    bool IsLate);

/// <summary>
/// The booking reference printed for customers: "ROAM-" + the first 16 hex digits of the
/// reservation id in four groups (ROAM-8F4B-92A1-0C7E-449D). The client formats it the same way.
/// </summary>
public static class BookingReference
{
    public static string Format(Guid reservationId)
    {
        var hex = reservationId.ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant();
        return $"ROAM-{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
    }

    /// <summary>
    /// Accepts the display reference (any case, with or without dashes/spaces) or a full GUID.
    /// Returns the full id, or the 16-hex-digit prefix to search for.
    /// </summary>
    public static bool TryParse(string? input, out Guid fullId, out string prefix)
    {
        fullId = Guid.Empty;
        prefix = string.Empty;
        var text = (input ?? string.Empty).Trim();

        if (Guid.TryParse(text, out fullId))
            return true;

        var compact = new string(text.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        if (compact.StartsWith("ROAM", StringComparison.Ordinal))
            compact = compact[4..];
        if (compact.Length != 16 || !compact.All(char.IsAsciiHexDigit))
            return false;

        prefix = compact;
        return true;
    }
}

/// <summary>All branches are in South Africa (SAST, UTC+2, no daylight saving): "a day" at the counter.</summary>
public static class BranchTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    public static (DateTimeOffset From, DateTimeOffset To) Day(DateOnly date)
    {
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), Offset);
        return (from, from.AddDays(1));
    }
}
