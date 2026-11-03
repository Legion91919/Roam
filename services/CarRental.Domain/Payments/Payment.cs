using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.ValueObjects;

namespace CarRental.Domain.Payments;

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Captured = 2,
    Voided = 3,
    Failed = 4
}

/// <summary>
/// AGGREGATE ROOT: the card payment for one reservation, following the usual card flow:
///   authorise (hold the money) at booking → capture (take it) when the car comes back
///                                          → void (release the hold) when the booking is cancelled.
/// </summary>
/// <remarks>
/// Invariants protected here:
///  * Amounts are positive and in the reservation's currency.
///  * Capture only from Authorized, and at most 15% above the authorised amount (covers late fees
///    and small extras; more than that needs a new authorisation).
///  * Void only from Authorized.
///  * Captured, Voided and Failed are final: no transition out of them.
/// The gateway call itself is not here (the Domain has no I/O). The use case asks the gateway and
/// hands its answer to these methods, which decide what that answer means for the payment.
/// </remarks>
public sealed class Payment : AggregateRoot<PaymentId>
{
    /// <summary>A capture may exceed the authorisation by at most this fraction (15%).</summary>
    public const decimal MaxCaptureOverAuthorization = 0.15m;

    public ReservationId ReservationId { get; private set; }
    public Money Authorized { get; private set; } = null!;
    public Money Captured { get; private set; } = null!;
    public PaymentStatus Status { get; private set; }
    public string GatewayReference { get; private set; } = string.Empty;
    public string? FailureReason { get; private set; }
    public CardSummary Card { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? AuthorizedAt { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }

    private Payment() { }   // EF Core

    /// <summary>The largest amount that may be captured: the authorisation plus 15%, rounded to cents.</summary>
    public Money CaptureLimit => (Authorized * (1 + MaxCaptureOverAuthorization)).Round();

    /// <summary>
    /// Records the gateway's answer to an authorisation request. Approved: the payment is Authorized.
    /// Declined: the payment is Failed and <see cref="PaymentDeclinedException"/> stops the booking.
    /// </summary>
    public static Payment Authorize(
        ReservationId reservationId, Money amount, CardSummary card,
        bool approved, string? gatewayReference, string? declineReason, DateTimeOffset now)
    {
        EnsurePositive(amount);

        var payment = new Payment
        {
            Id = PaymentId.New(),
            ReservationId = reservationId,
            Authorized = amount,
            Captured = Money.Zero(amount.Currency),
            Card = card,
            Status = PaymentStatus.Pending,
            CreatedAt = now,
        };

        if (!approved)
        {
            payment.Fail(declineReason ?? "declined", now);
            throw new PaymentDeclinedException(payment.FailureReason!);
        }

        if (string.IsNullOrWhiteSpace(gatewayReference))
            throw new DomainException("payment.gateway_reference_required", "An approved authorisation must have a gateway reference.");

        payment.GatewayReference = gatewayReference;
        payment.Status = PaymentStatus.Authorized;
        payment.AuthorizedAt = now;
        payment.Raise(new PaymentAuthorized(payment.Id, reservationId, amount, card, now));
        return payment;
    }

    /// <summary>
    /// Checks a capture is allowed BEFORE the gateway is asked to take the money. Also run by
    /// <see cref="Capture"/>, so the rule holds even if a caller skips this step.
    /// </summary>
    public void EnsureCanCapture(Money amount)
    {
        EnsureTransitionFrom(PaymentStatus.Authorized, "captured");
        EnsurePositive(amount);
        EnsureSameCurrency(amount);
        if (amount.Amount > CaptureLimit.Amount)
            throw new DomainException("payment.capture_exceeds_authorization",
                $"Cannot capture {amount}: at most {CaptureLimit} (the authorised {Authorized} plus {MaxCaptureOverAuthorization:P0}).");
    }

    /// <summary>Records the gateway's answer to a capture. A declined capture leaves the payment Failed.</summary>
    public void Capture(Money amount, bool approved, string? declineReason, DateTimeOffset now)
    {
        EnsureCanCapture(amount);

        if (!approved)
        {
            Fail(declineReason ?? "capture_declined", now);
            return;
        }

        Captured = amount;
        CapturedAt = now;
        Status = PaymentStatus.Captured;
        Raise(new PaymentCaptured(Id, ReservationId, amount, now));
    }

    public void EnsureCanVoid() => EnsureTransitionFrom(PaymentStatus.Authorized, "voided");

    /// <summary>Releases the hold. A declined void leaves the payment Failed (the hold then expires at the bank).</summary>
    public void Void(bool approved, string? declineReason, DateTimeOffset now)
    {
        EnsureCanVoid();

        if (!approved)
        {
            Fail(declineReason ?? "void_declined", now);
            return;
        }

        Status = PaymentStatus.Voided;
        VoidedAt = now;
        Raise(new PaymentVoided(Id, ReservationId, Authorized, now));
    }

    private void Fail(string reason, DateTimeOffset now)
    {
        Status = PaymentStatus.Failed;
        FailureReason = reason;
        Raise(new PaymentFailed(Id, ReservationId, reason, now));
    }

    private void EnsureTransitionFrom(PaymentStatus required, string action)
    {
        if (Status != required)
            throw new DomainException("payment.invalid_transition", $"A {Status} payment cannot be {action}.");
    }

    private static void EnsurePositive(Money amount)
    {
        if (amount.Amount <= 0m)
            throw new DomainException("payment.invalid_amount", "A payment amount must be greater than zero.");
    }

    private void EnsureSameCurrency(Money amount)
    {
        if (amount.Currency != Authorized.Currency)
            throw new DomainException("money.currency_mismatch", $"Cannot capture {amount.Currency} against a {Authorized.Currency} authorisation.");
    }
}

/// <summary>The card issuer refused the authorisation. The API answers 402 Payment Required.</summary>
public sealed class PaymentDeclinedException(string reason)
    : DomainException("payment.declined", $"The card was declined ({reason}).")
{
    /// <summary>The gateway's reason code, e.g. "card_declined" or "insufficient_funds".</summary>
    public string Reason { get; } = reason;
}

public sealed record PaymentAuthorized(PaymentId PaymentId, ReservationId ReservationId, Money Amount, CardSummary Card, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentCaptured(PaymentId PaymentId, ReservationId ReservationId, Money Amount, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentVoided(PaymentId PaymentId, ReservationId ReservationId, Money Amount, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentFailed(PaymentId PaymentId, ReservationId ReservationId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
