using CarRental.Domain.Common;
using CarRental.Domain.Eligibility;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using CarRental.Domain.Payments;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;

namespace CarRental.Domain.Rentals;

public enum RentalStatus
{
    Open = 0,
    Closed = 1
}

/// <summary>
/// AGGREGATE ROOT: a car that is physically out with a customer.
/// </summary>
/// <remarks>
/// Reservation and Rental are separate aggregates on purpose: they have different lifecycles,
/// different invariants and different people changing them (the customer books online, the
/// counter agent hands over keys). A reservation can exist without ever becoming a rental (no-show,
/// cancellation). They reference each other by ID only, never by object reference.
///
/// Invariants protected here:
///  * Checkout only from a confirmed reservation, inside the pickup window.
///  * The car handed over is a real fleet Vehicle: at the pickup branch, Available, in the booked
///    category (or a higher one when the agent gives a free upgrade the driver is eligible for).
///  * The odometer can never go backwards (mileage fraud / typing errors).
///  * A rental can only be returned once.
///  * Late returns pay a fee after a grace period, based on the rate locked in at checkout.
/// </remarks>
// SOLID (S): Reservation and Rental change for different reasons (booking vs. handover/return),
//           so they are separate classes and separate aggregates.
public sealed class Rental : AggregateRoot<RentalId>
{
    public static readonly TimeSpan EarlyPickupWindow = TimeSpan.FromHours(2);
    public static readonly TimeSpan ReturnGracePeriod = TimeSpan.FromMinutes(59);
    public const decimal LateFeeMultiplier = 1.5m;

    public ReservationId ReservationId { get; private set; }
    public VehicleId VehicleId { get; private set; }
    public VehicleCategory Category { get; private set; }
    public BranchId ReturnBranchId { get; private set; }
    public string VehicleRegistration { get; private set; } = string.Empty;
    public Money DailyRate { get; private set; } = null!;
    public DateTimeOffset PickedUpAt { get; private set; }
    public DateTimeOffset DueBackAt { get; private set; }
    public int StartOdometerKm { get; private set; }
    public DateTimeOffset? ReturnedAt { get; private set; }
    public int? EndOdometerKm { get; private set; }
    public Money LateFee { get; private set; } = null!;
    public RentalStatus Status { get; private set; }

    public int? DistanceKm => EndOdometerKm - StartOdometerKm;

    private Rental() { }   // EF Core

    /// <param name="checkoutInspection">The completed Checkout inspection of THIS car for THIS booking; its odometer is the start reading.</param>
    /// <param name="freeUpgrade">The agent ticked "free upgrade": a higher category is allowed, at the booked price.</param>
    public static Rental CheckOut(Reservation reservation, Vehicle vehicle, InspectionReport? checkoutInspection, Payment? payment, Money dailyRate, bool freeUpgrade, DateTimeOffset now)
    {
        EnsurePaymentHeld(reservation, payment);
        var startOdometerKm = RequireInspection(checkoutInspection, InspectionKind.Checkout, reservation.Id, vehicle.Id);
        if (startOdometerKm < 0)
            throw new DomainException("rental.negative_odometer", "Odometer reading cannot be negative.");
        if (now < reservation.Period.PickupAt - EarlyPickupWindow)
            throw new DomainException("rental.too_early", "The car can be collected at most 2 hours before the pickup time.");
        if (now >= reservation.Period.ReturnAt)
            throw new DomainException("rental.reservation_expired", "The reservation period has already ended.");
        if (vehicle.CurrentBranchId != reservation.PickupBranchId)
            throw new DomainException("rental.vehicle_wrong_branch", $"{vehicle.Registration} is not at the pickup branch.");

        var isUpgrade = vehicle.Category > reservation.Category;
        if (vehicle.Category != reservation.Category && !(freeUpgrade && isUpgrade))
            throw new DomainException("rental.category_mismatch",
                $"{vehicle.Registration} is {vehicle.Category}; the booking is for {reservation.Category}. Choose a {reservation.Category} car or tick free upgrade.");
        if (isUpgrade)   // an upgrade to Luxury still needs a 25+ driver
            DriverEligibilityPolicy.EnsureEligible(reservation.Driver, vehicle.Category, DateOnly.FromDateTime(reservation.Period.PickupAt.Date));

        reservation.MarkCheckedOut();   // throws if the reservation is not Confirmed

        var rental = new Rental
        {
            Id = RentalId.New(),
            ReservationId = reservation.Id,
            VehicleId = vehicle.Id,
            Category = reservation.Category,
            ReturnBranchId = reservation.ReturnBranchId,
            VehicleRegistration = vehicle.Registration.Value,
            DailyRate = dailyRate,
            PickedUpAt = now,
            DueBackAt = reservation.Period.ReturnAt,
            StartOdometerKm = startOdometerKm,
            LateFee = Money.Zero(dailyRate.Currency),
            Status = RentalStatus.Open,
        };

        vehicle.AssignToRental(rental.Id, startOdometerKm, now);   // throws if not Available or the odometer went backwards
        rental.Raise(new VehicleCheckedOut(rental.Id, reservation.Id, rental.VehicleRegistration, now));
        return rental;
    }

    /// <summary>
    /// No keys without money on hold: a pay-at-branch booking needs the hold the agent took at the
    /// counter. (A card booking's hold was taken at booking; bookings from before payments existed
    /// have none and are still allowed.)
    /// </summary>
    private static void EnsurePaymentHeld(Reservation reservation, Payment? payment)
    {
        if (reservation.PaymentOption != PaymentOption.PayAtBranch)
            return;
        if (payment is null || payment.ReservationId != reservation.Id || payment.Status != PaymentStatus.Authorized)
            throw new DomainException("rental.payment_required", "Take the payment at the counter before handing over the keys.");
    }

    /// <returns>The late fee charged (zero when on time).</returns>
    /// <remarks>The car is checked back in too, at the booking's return branch (same transaction).
    /// The end odometer comes from the completed Return inspection.</remarks>
    public Money Return(Vehicle vehicle, InspectionReport? returnInspection, DateTimeOffset returnedAt)
    {
        var endOdometerKm = RequireInspection(returnInspection, InspectionKind.Return, ReservationId, VehicleId);
        if (vehicle.Id != VehicleId)
            throw new DomainException("rental.wrong_vehicle", $"This rental is for {VehicleRegistration}, not {vehicle.Registration}.");
        if (Status != RentalStatus.Open)
            throw new DomainException("rental.already_returned", "This rental has already been returned.");
        if (endOdometerKm < StartOdometerKm)
            throw new DomainException("rental.odometer_decreased",
                $"Odometer cannot decrease: checked out at {StartOdometerKm} km, returned at {endOdometerKm} km.");
        if (returnedAt < PickedUpAt)
            throw new DomainException("rental.return_before_pickup", "Return time cannot be before the pickup time.");

        vehicle.ReturnFromRental(endOdometerKm, ReturnBranchId, returnedAt);
        LateFee = CalculateLateFee(DueBackAt, returnedAt, DailyRate);
        EndOdometerKm = endOdometerKm;
        ReturnedAt = returnedAt;
        Status = RentalStatus.Closed;

        Raise(new VehicleReturned(Id, ReservationId, returnedAt, endOdometerKm - StartOdometerKm, LateFee));
        return LateFee;
    }

    /// <summary>
    /// Keys only change hands after a completed inspection of this car for this booking
    /// (rental.inspection_required). Returns the odometer recorded on it.
    /// </summary>
    private static int RequireInspection(InspectionReport? inspection, InspectionKind kind, ReservationId reservationId, VehicleId vehicleId)
    {
        if (inspection is null || inspection.Kind != kind || inspection.Status != InspectionStatus.Completed
            || inspection.ReservationId != reservationId || inspection.VehicleId != vehicleId || inspection.OdometerKm is null)
            throw new DomainException("rental.inspection_required",
                $"Complete the {kind.ToString().ToLowerInvariant()} inspection of this car before {(kind == InspectionKind.Checkout ? "handing over the keys" : "closing the rental")}.");
        return inspection.OdometerKm.Value;
    }

    /// <summary>Overdue = still out after the due time plus the 59-minute grace (the point a late fee starts).</summary>
    public static bool IsOverdue(DateTimeOffset dueBackAt, DateTimeOffset now) => now - dueBackAt > ReturnGracePeriod;

    /// <summary>
    /// Late fee: nothing within the grace period, then 1.5x the daily rate for EVERY STARTED 24 hours
    /// late (counted from the due time, not from the end of the grace period).
    /// 59 min late = R0, 60 min late = 1 day, 24h late = 1 day, 24h01 late = 2 days.
    /// </summary>
    public static Money CalculateLateFee(DateTimeOffset dueBackAt, DateTimeOffset returnedAt, Money dailyRate)
    {
        var late = returnedAt - dueBackAt;
        if (late <= ReturnGracePeriod)
            return Money.Zero(dailyRate.Currency);

        // Integer ceiling on ticks avoids floating-point surprises of Math.Ceiling(TotalHours / 24).
        var startedDays = (int)((late.Ticks + TimeSpan.TicksPerDay - 1) / TimeSpan.TicksPerDay);
        return (dailyRate * LateFeeMultiplier * startedDays).Round();
    }
}

public sealed record VehicleCheckedOut(RentalId RentalId, ReservationId ReservationId, string VehicleRegistration, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record VehicleReturned(RentalId RentalId, ReservationId ReservationId, DateTimeOffset OccurredAt, int DistanceKm, Money LateFee) : IDomainEvent;
