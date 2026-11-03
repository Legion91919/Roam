using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Payments;
using CarRental.Application.Pricing;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;

namespace CarRental.Application.Reservations.CreateReservation;

/// <summary>
/// USE CASE: the signed-in customer books a car category at a branch for a period.
/// A command is a plain, immutable message: the INTENT, with primitive data from the outside world.
/// Notice what is NOT here: customer id, date of birth, licence date and loyalty tier. Those come
/// from the signed-in user's verified profile, so a client cannot book as someone else or claim
/// a discount it doesn't have. There is no card number either: only the gateway's single-use
/// <see cref="PaymentToken"/> (PCI scope stays with the gateway). With
/// <see cref="PaymentOption.PayAtBranch"/> there is no token at all: the agent takes the card at the counter.
/// </summary>
public sealed record CreateReservationCommand(
    VehicleCategory Category,
    Guid PickupBranchId,
    Guid ReturnBranchId,
    DateTimeOffset PickupAt,
    DateTimeOffset ReturnAt,
    string? AcceptedTermsVersion,
    Extras Extras = Extras.None,
    string? PaymentToken = null,
    PaymentOption PaymentOption = PaymentOption.Card)
    : ICommand<CreateReservationResult>, IRetryOnConcurrencyConflict
{
    /// <summary>
    /// The id the new reservation will get, fixed when the command is created. Because the retry
    /// behaviour re-runs the SAME command, every attempt uses the same id, and therefore the same
    /// payment idempotency key: a lost race never authorises the card twice. A client may also send
    /// its own value so that its own retries are safe.
    /// </summary>
    public Guid ReservationId { get; init; } = Guid.NewGuid();
}

/// <summary><see cref="Payment"/> is null when the customer pays at the branch.</summary>
public sealed record CreateReservationResult(Guid ReservationId, QuoteDto Price, PaymentSummaryDto? Payment, string PaymentOption);
