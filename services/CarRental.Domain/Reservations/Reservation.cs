using CarRental.Domain.Common;
using CarRental.Domain.Eligibility;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Pricing;
using CarRental.Domain.Terms;
using CarRental.Domain.ValueObjects;

namespace CarRental.Domain.Reservations;

public enum ReservationStatus
{
    Confirmed = 0,
    Cancelled = 1,
    CheckedOut = 2,
    NoShow = 3
}

/// <summary>
/// How the customer pays. <see cref="Card"/>: the total is held on their card when they book.
/// <see cref="PayAtBranch"/>: nothing is taken online; the agent takes the card at the counter
/// (a hold for the booking total) before handing over the keys.
/// </summary>
public enum PaymentOption
{
    Card = 0,
    PayAtBranch = 1
}

public enum CancelledBy
{
    Customer = 0,
    Agent = 1
}

/// <summary>Who asks to cancel, why, and when. Built by the use case from the signed-in user (never from the request body).</summary>
public sealed record CancellationRequest(CancelledBy By, string ActorUserId, string? Reason, DateTimeOffset Now)
{
    public static CancellationRequest ByCustomer(string userId, DateTimeOffset now) => new(CancelledBy.Customer, userId, null, now);
    public static CancellationRequest ByAgent(string userId, string? reason, DateTimeOffset now) => new(CancelledBy.Agent, userId, reason, now);
}

/// <summary>VALUE OBJECT stored on a cancelled or no-show reservation: evidence of who ended it and why.</summary>
public sealed record CancellationDetails(CancelledBy By, string ByUserId, string? Reason, DateTimeOffset At);

/// <summary>
/// AGGREGATE ROOT: a customer's promise of a car in a category, at a branch, for a period, at a price.
/// </summary>
/// <remarks>
/// Invariants protected here:
///  * The pickup is not in the past.
///  * The driver is eligible (age, licence, category).
///  * The price is agreed at booking and stored (a snapshot); it never changes afterwards.
///  * The current rental terms were accepted; the accepted version is stored with the booking.
///  * Only a confirmed reservation can be cancelled. A customer only before pickup; an agent at any
///    time before checkout, but always with a reason.
///  * A no-show can be recorded by an agent only from 2 hours after the pickup time.
///  * Only a confirmed reservation can be checked out, and only once.
///  * Pay at the branch: the agent can take the payment at the counter once, while the booking is
///    confirmed; the keys are not handed over until that hold is in place (Rental.CheckOut).
/// "Now" is passed in, never read from DateTime.Now, so the rules are deterministic and testable.
/// </remarks>
// SOLID (S): the Reservation guards reservation rules only. It delegates eligibility to
//           DriverEligibilityPolicy and pricing to RentalPricer instead of doing everything itself.
public sealed class Reservation : AggregateRoot<ReservationId>
{
    public CustomerId CustomerId { get; private set; }
    public VehicleCategory Category { get; private set; }
    public BranchId PickupBranchId { get; private set; }
    public BranchId ReturnBranchId { get; private set; }
    public RentalPeriod Period { get; private set; } = null!;
    public DriverProfile Driver { get; private set; } = null!;
    public Extras Extras { get; private set; }
    public Money QuotedTotal { get; private set; } = null!;
    public TermsAcceptance Terms { get; private set; } = null!;
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public CancellationDetails? Cancellation { get; private set; }
    public PaymentOption PaymentOption { get; private set; }

    /// <summary>An agent may record a no-show from this long after the pickup time.</summary>
    public static readonly TimeSpan NoShowAfter = TimeSpan.FromHours(2);

    private Reservation() { }   // EF Core

    public static Reservation Create(
        CustomerId customerId,
        VehicleCategory category,
        BranchId pickupBranchId,
        BranchId returnBranchId,
        RentalPeriod period,
        DriverProfile driver,
        Extras extras,
        PriceQuote quote,
        TermsAcceptance terms,
        DateTimeOffset now,
        ReservationId? id = null,
        PaymentOption paymentOption = PaymentOption.Card)
    {
        if (!Enum.IsDefined(paymentOption))
            throw new DomainException("reservation.invalid_payment_option", "Choose to pay by card now or at the branch.");

        if (period.PickupAt < now)
            throw new DomainException("reservation.pickup_in_past", "Pickup time cannot be in the past.");

        DriverEligibilityPolicy.EnsureEligible(driver, category, DateOnly.FromDateTime(period.PickupAt.Date));

        if (quote.Total.IsNegative)
            throw new DomainException("reservation.negative_total", "A reservation total cannot be negative.");

        var reservation = new Reservation
        {
            // The caller may choose the id up front (it doubles as the payment idempotency key).
            Id = id ?? ReservationId.New(),
            CustomerId = customerId,
            Category = category,
            PickupBranchId = pickupBranchId,
            ReturnBranchId = returnBranchId,
            Period = period,
            Driver = driver,
            Extras = extras,
            QuotedTotal = quote.Total,
            Terms = terms,
            Status = ReservationStatus.Confirmed,
            CreatedAt = now,
            PaymentOption = paymentOption,
        };

        reservation.Raise(new ReservationCreated(
            reservation.Id, customerId, pickupBranchId, category, period, quote.Total, now));

        return reservation;
    }

    public void Cancel(CancellationRequest request)
    {
        if (Status != ReservationStatus.Confirmed)
            throw new DomainException("reservation.not_cancellable", $"A {Status} reservation cannot be cancelled.");

        string? reason = null;
        if (request.By == CancelledBy.Customer)
        {
            if (request.Now >= Period.PickupAt)
                throw new DomainException("reservation.too_late_to_cancel", "A reservation cannot be cancelled after the pickup time.");
        }
        else
        {
            reason = RequireReason(request.Reason);
        }

        End(ReservationStatus.Cancelled, new CancellationDetails(request.By, request.ActorUserId, reason, request.Now));
    }

    /// <summary>The customer never came. Only an agent, only from 2 hours after the pickup time.</summary>
    public void MarkNoShow(string agentUserId, DateTimeOffset now)
    {
        if (Status != ReservationStatus.Confirmed)
            throw new DomainException("reservation.not_cancellable", $"A {Status} reservation cannot be marked as a no-show.");
        if (now < Period.PickupAt + NoShowAfter)
            throw new DomainException("reservation.no_show_too_early",
                $"A no-show can be recorded from {NoShowAfter.TotalHours:0} hours after the pickup time.");

        End(ReservationStatus.NoShow, new CancellationDetails(CancelledBy.Agent, agentUserId, "No-show", now));
    }

    private void End(ReservationStatus status, CancellationDetails details)
    {
        Status = status;
        CancelledAt = details.At;
        Cancellation = details;
        Raise(new ReservationCancelled(Id, PickupBranchId, Category, Period, details.By, details.Reason, status == ReservationStatus.NoShow, details.At));
    }

    private static string RequireReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 200
            ? throw new DomainException("reservation.cancel_reason_required", "Staff must give a reason (max 200 characters) to cancel a booking.")
            : reason.Trim();

    /// <summary>
    /// Pay at the branch: may the agent take the payment now? Only for a confirmed pay-at-branch
    /// booking, and only once (a declined card saves nothing, so the agent can try another card).
    /// </summary>
    public void EnsureCanTakeCounterPayment(bool holdAlreadyTaken)
    {
        if (Status != ReservationStatus.Confirmed)
            throw new DomainException("reservation.not_payable", $"A {Status} reservation cannot be paid for.");
        if (PaymentOption != PaymentOption.PayAtBranch)
            throw new DomainException("payment.paid_online", "This booking was paid by card online: the hold is already on the customer's card.");
        if (holdAlreadyTaken)
            throw new DomainException("payment.already_taken", "The payment for this booking has already been taken.");
    }

    /// <summary>Called only by <see cref="Rentals.Rental.CheckOut"/> (internal: same assembly).</summary>
    internal void MarkCheckedOut()
    {
        if (Status != ReservationStatus.Confirmed)
            throw new DomainException("reservation.not_checkoutable", $"A {Status} reservation cannot be checked out.");

        Status = ReservationStatus.CheckedOut;
    }
}

public sealed record ReservationCreated(
    ReservationId ReservationId,
    CustomerId CustomerId,
    BranchId PickupBranchId,
    VehicleCategory Category,
    RentalPeriod Period,
    Money Total,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Raised for cancellations and no-shows alike (both give the cars back), with who and why.</summary>
public sealed record ReservationCancelled(
    ReservationId ReservationId,
    BranchId PickupBranchId,
    VehicleCategory Category,
    RentalPeriod Period,
    CancelledBy By,
    string? Reason,
    bool NoShow,
    DateTimeOffset OccurredAt) : IDomainEvent;
