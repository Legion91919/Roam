using CarRental.Application.Abstractions.Payments;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Domain.Inventory;
using CarRental.Domain.Pricing;
using CarRental.Domain.Reservations;

namespace CarRental.Application.Reservations.Cancellation;

/// <summary>
/// What every ended booking needs once the Domain has accepted the cancellation or no-show: give the
/// cars back to inventory and settle the card. Shared by the customer cancel, agent cancel and no-show
/// use cases so the three cannot drift apart. Orchestration only; the rules are in the aggregates.
/// </summary>
public sealed class BookingRelease(
    ICategoryInventoryRepository inventory,
    IPaymentRepository payments,
    IPaymentGateway gateway,
    INoShowFeePolicy noShowFees)
{
    public async Task ReleaseInventoryAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var days = await inventory.GetRangeAsync(
            reservation.PickupBranchId, reservation.Category, reservation.Period.FirstDay, reservation.Period.LastDay, cancellationToken);
        InventoryAllocation.Release(reservation.PickupBranchId, reservation.Category, reservation.Period, days);
    }

    /// <summary>Cancelled before checkout: release the whole hold.</summary>
    public async Task VoidPaymentAsync(Reservation reservation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Bookings made before payments existed have no payment row.
        var payment = await payments.GetByReservationAsync(reservation.Id, cancellationToken);
        if (payment is null) return;

        payment.EnsureCanVoid();
        var result = await gateway.VoidAsync(payment.GatewayReference, PaymentIdempotencyKey.Void(reservation.Id), cancellationToken);
        payment.Void(result.Approved, result.ReasonCode, now);
    }

    /// <summary>No-show: capture the no-show fee only; the gateway releases the rest of the hold.</summary>
    public async Task ChargeNoShowFeeAsync(Reservation reservation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var payment = await payments.GetByReservationAsync(reservation.Id, cancellationToken);
        if (payment is null) return;

        var fee = noShowFees.FeeFor(reservation);
        payment.EnsureCanCapture(fee);
        var result = await gateway.CaptureAsync(payment.GatewayReference, fee, PaymentIdempotencyKey.Capture(reservation.Id), cancellationToken);
        payment.Capture(fee, result.Approved, result.ReasonCode, now);
    }
}
