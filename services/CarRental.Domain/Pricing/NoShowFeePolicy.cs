using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;

namespace CarRental.Domain.Pricing;

/// <summary>
/// POLICY: what a no-show costs. An interface so the business can change the policy (a flat fee,
/// a percentage, free for Gold members) by adding a class and changing one DI line: the no-show use
/// case never changes.
/// </summary>
// SOLID (O): open for new no-show policies, closed for modification of the use case.
public interface INoShowFeePolicy
{
    Money FeeFor(Reservation reservation);
}

/// <summary>
/// Standard policy: one day at the booked category's daily rate, never more than the booking total
/// (so it always fits inside the card authorisation). The rest of the hold is released.
/// </summary>
public sealed class OneDayRateNoShowFee(RateCard rates) : INoShowFeePolicy
{
    public Money FeeFor(Reservation reservation) =>
        Money.Min(rates.DailyRate(reservation.Category), reservation.QuotedTotal).Round();
}
