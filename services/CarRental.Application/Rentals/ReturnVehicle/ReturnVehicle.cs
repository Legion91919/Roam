using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.Payments;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Common.Exceptions;
using CarRental.Application.Payments;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Rentals.ReturnVehicle;

/// <summary>
/// Car comes back, after the return inspection is completed: close the rental (odometer from the
/// inspection), check the car in at the return branch, charge any late fee and capture the payment.
/// </summary>
public sealed record ReturnVehicleCommand(Guid RentalId) : ICommand<ReturnVehicleResult>;

public sealed record ReturnVehicleResult(
    Guid RentalId,
    int DistanceKm,
    decimal LateFee,
    string Currency,
    DateTimeOffset ReturnedAt,
    decimal AmountDue,
    PaymentSummaryDto? Payment);

public sealed class ReturnVehicleValidator : AbstractValidator<ReturnVehicleCommand>
{
    public ReturnVehicleValidator() => RuleFor(c => c.RentalId).NotEmpty();
}

/// <summary>
/// Orchestrates the return: the Rental decides the late fee (and refuses without a completed return
/// inspection); the Vehicle is checked back in; the Payment decides whether booking total + fee may be
/// captured (at most 15% above the hold); the gateway takes the money. All in one transaction.
/// </summary>
public sealed class ReturnVehicleHandler(
    IRentalRepository rentals,
    IReservationRepository reservations,
    IVehicleRepository vehicles,
    IInspectionRepository inspections,
    IPaymentRepository payments,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
    : IRequestHandler<ReturnVehicleCommand, ReturnVehicleResult>
{
    public async Task<ReturnVehicleResult> Handle(ReturnVehicleCommand command, CancellationToken cancellationToken)
    {
        var rental = await rentals.GetAsync(new RentalId(command.RentalId), cancellationToken)
            ?? throw new NotFoundException("Rental", command.RentalId);
        var reservation = await reservations.GetAsync(rental.ReservationId, cancellationToken)
            ?? throw new NotFoundException("Reservation", rental.ReservationId.Value);
        var vehicle = await vehicles.GetAsync(rental.VehicleId, cancellationToken)
            ?? throw new NotFoundException("Vehicle", rental.VehicleId.Value);
        var inspection = await inspections.GetAsync(reservation.Id, InspectionKind.Return, cancellationToken);

        var returnedAt = clock.GetUtcNow();
        var lateFee = rental.Return(vehicle, inspection, returnedAt);   // inspection required; odometer never decreases
        var amountDue = (reservation.QuotedTotal + lateFee).Round();

        // Bookings made before payments existed have no payment row: nothing to capture.
        var payment = await payments.GetByReservationAsync(reservation.Id, cancellationToken);
        if (payment is not null)
        {
            payment.EnsureCanCapture(amountDue);   // check the 15% rule BEFORE asking the gateway to take money
            var result = await gateway.CaptureAsync(payment.GatewayReference, amountDue, PaymentIdempotencyKey.Capture(reservation.Id), cancellationToken);
            payment.Capture(amountDue, result.Approved, result.ReasonCode, returnedAt);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ReturnVehicleResult(rental.Id.Value, rental.DistanceKm ?? 0, lateFee.Amount, lateFee.Currency, returnedAt,
            amountDue.Amount, payment is null ? null : PaymentSummaryDto.From(payment));
    }
}
