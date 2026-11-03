using CarRental.Domain.Common;

namespace CarRental.Domain.ValueObjects;

/// <summary>
/// VALUE OBJECT: the time window a car is booked for.
/// </summary>
/// <remarks>
/// It is impossible to create an invalid period (return before pickup, longer than 90 days):
/// the only public way in is <see cref="Create"/>, which validates. Code that receives a
/// RentalPeriod therefore never has to re-check it ("make illegal states unrepresentable").
///
/// It also owns the period maths, so the rules live in ONE place:
///  * <see cref="ChargeableDays"/>: every started 24 hours is a day, after a grace period.
///  * <see cref="OccupiedDates"/>: which inventory days the booking consumes.
///  * <see cref="Overlaps"/>: the classic interval test (a.start &lt; b.end AND b.start &lt; a.end).
/// </remarks>
public sealed record RentalPeriod
{
    public static readonly TimeSpan MinimumLength = TimeSpan.FromHours(1);
    public static readonly TimeSpan MaximumLength = TimeSpan.FromDays(90);

    public DateTimeOffset PickupAt { get; private init; }
    public DateTimeOffset ReturnAt { get; private init; }

    // Private: used by Create and by EF Core (it binds constructor parameters by name).
    private RentalPeriod(DateTimeOffset pickupAt, DateTimeOffset returnAt)
    {
        PickupAt = pickupAt;
        ReturnAt = returnAt;
    }

    public static RentalPeriod Create(DateTimeOffset pickupAt, DateTimeOffset returnAt)
    {
        if (returnAt <= pickupAt)
            throw new DomainException("period.return_before_pickup", "Return time must be after pickup time.");

        var length = returnAt - pickupAt;
        if (length < MinimumLength)
            throw new DomainException("period.too_short", "A rental must be at least one hour long.");
        if (length > MaximumLength)
            throw new DomainException("period.too_long", "A rental cannot be longer than 90 days.");

        return new RentalPeriod(pickupAt, returnAt);
    }

    public TimeSpan Duration => ReturnAt - PickupAt;

    /// <summary>First and last calendar day (UTC) that this booking occupies in inventory.</summary>
    public DateOnly FirstDay => DateOnly.FromDateTime(PickupAt.UtcDateTime);
    public DateOnly LastDay => DateOnly.FromDateTime(ReturnAt.UtcDateTime);

    /// <summary>
    /// Charging rule: every STARTED 24 hours counts as a day, but the first
    /// <paramref name="grace"/> of a new day is free. Minimum one day.
    /// 24h00 = 1 day, 24h59 = 1 day (grace), 25h00 = 2 days.
    /// </summary>
    public int ChargeableDays(TimeSpan grace)
    {
        var fullDays = (int)(Duration.Ticks / TimeSpan.TicksPerDay);
        var remainder = Duration - TimeSpan.FromDays(fullDays);
        var days = remainder > grace ? fullDays + 1 : fullDays;
        return Math.Max(1, days);
    }

    /// <summary>
    /// Every UTC calendar day the car is away, including the pickup and return days.
    /// Daily granularity is deliberately conservative: a car returned at 10:00 still counts on
    /// that day. A production system could use hourly slots or allocate specific vehicles.
    /// </summary>
    public IEnumerable<DateOnly> OccupiedDates()
    {
        for (var day = FirstDay; day <= LastDay; day = day.AddDays(1))
            yield return day;
    }

    public bool Overlaps(RentalPeriod other) => PickupAt < other.ReturnAt && other.PickupAt < ReturnAt;

    public override string ToString() => $"{PickupAt:u} to {ReturnAt:u}";
}
