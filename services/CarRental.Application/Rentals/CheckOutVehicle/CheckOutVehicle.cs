using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Common.Exceptions;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using CarRental.Domain.Pricing;
using CarRental.Domain.Rentals;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Rentals.CheckOutVehicle;

/// <summary>
/// Counter agent hands over the keys of a specific fleet car: the reservation becomes a rental.
/// The start odometer comes from the completed checkout inspection of that car.
/// </summary>
public sealed record CheckOutVehicleCommand(Guid ReservationId, Guid VehicleId, bool FreeUpgrade = false) : ICommand<Guid>;

public sealed class CheckOutVehicleValidator : AbstractValidator<CheckOutVehicleCommand>
{
    public CheckOutVehicleValidator()
    {
        RuleFor(c => c.ReservationId).NotEmpty();
        RuleFor(c => c.VehicleId).NotEmpty();
    }
}

public sealed class CheckOutVehicleHandler(
    IReservationRepository reservations,
    IVehicleRepository vehicles,
    IInspectionRepository inspections,
    IPaymentRepository payments,
    IRentalRepository rentals,
    IUnitOfWork unitOfWork,
    RateCard rates,
    TimeProvider clock)
    : IRequestHandler<CheckOutVehicleCommand, Guid>
{
    public async Task<Guid> Handle(CheckOutVehicleCommand command, CancellationToken cancellationToken)
    {
        var reservation = await reservations.GetAsync(new ReservationId(command.ReservationId), cancellationToken)
            ?? throw new NotFoundException("Reservation", command.ReservationId);
        var vehicle = await vehicles.GetAsync(new VehicleId(command.VehicleId), cancellationToken)
            ?? throw new NotFoundException("Vehicle", command.VehicleId);
        var inspection = await inspections.GetAsync(reservation.Id, InspectionKind.Checkout, cancellationToken);
        var payment = await payments.GetByReservationAsync(reservation.Id, cancellationToken);

        // The Domain decides: completed inspection of this car, money on hold (pay at the branch),
        // category/upgrade, branch, pickup window.
        // The daily rate of the BOOKED category is locked in (a free upgrade costs nothing extra).
        var rental = Rental.CheckOut(reservation, vehicle, inspection, payment, rates.DailyRate(reservation.Category),
            command.FreeUpgrade, clock.GetUtcNow());
        inspection!.LinkRental(rental.Id);

        rentals.Add(rental);
        await unitOfWork.SaveChangesAsync(cancellationToken);   // reservation + vehicle + rental + inspection, atomically

        return rental.Id.Value;
    }
}
