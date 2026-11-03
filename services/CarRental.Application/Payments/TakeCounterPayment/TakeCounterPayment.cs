using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.Payments;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Common.Exceptions;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Payments;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Payments.TakeCounterPayment;

/// <summary>
/// USE CASE (Agent role): the customer chose to pay at the branch, so at the counter the agent takes
/// their card: a hold for the booking total, exactly like the hold a card booking gets online. From
/// then on the booking follows the same path (capture total + late fee at return, void on cancel).
/// As online, the card details go only to the gateway's hosted fields; this command carries the
/// single-use token, never a card number.
/// </summary>
public sealed record TakeCounterPaymentCommand(Guid ReservationId, string PaymentToken) : ICommand<PaymentSummaryDto>;

public sealed class TakeCounterPaymentValidator : AbstractValidator<TakeCounterPaymentCommand>
{
    public TakeCounterPaymentValidator()
    {
        RuleFor(c => c.ReservationId).NotEmpty();
        RuleFor(c => c.PaymentToken).NotEmpty().MaximumLength(100);
    }
}

/// <remarks>
/// Thin, like every handler: the Reservation decides whether a counter payment is allowed (confirmed,
/// pay-at-branch, not taken yet) and the Payment decides what the gateway's answer means. A decline
/// throws payment.declined (402) before anything is saved, so the agent can simply try another card.
/// </remarks>
public sealed class TakeCounterPaymentHandler(
    IReservationRepository reservations,
    IPaymentRepository payments,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
    : IRequestHandler<TakeCounterPaymentCommand, PaymentSummaryDto>
{
    public async Task<PaymentSummaryDto> Handle(TakeCounterPaymentCommand command, CancellationToken cancellationToken)
    {
        var reservation = await reservations.GetAsync(new ReservationId(command.ReservationId), cancellationToken)
            ?? throw new NotFoundException("Reservation", command.ReservationId);
        var existing = await payments.GetByReservationAsync(reservation.Id, cancellationToken);

        reservation.EnsureCanTakeCounterPayment(holdAlreadyTaken: existing is not null);

        var now = clock.GetUtcNow();
        var authorization = await gateway.AuthorizeAsync(
            command.PaymentToken, reservation.QuotedTotal, PaymentIdempotencyKey.AuthorizeAtCounter(reservation.Id, command.PaymentToken), cancellationToken);
        var payment = Payment.Authorize(reservation.Id, reservation.QuotedTotal,
            new CardSummary(authorization.CardBrand ?? "Card", authorization.CardLast4 ?? "0000"),
            authorization.Approved, authorization.Reference, authorization.ReasonCode, now);

        payments.Add(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken);   // one payment per reservation (unique index) guards a double click

        return PaymentSummaryDto.From(payment);
    }
}
