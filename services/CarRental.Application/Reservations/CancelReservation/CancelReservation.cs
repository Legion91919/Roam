using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Common.Customers;
using CarRental.Application.Common.Exceptions;
using CarRental.Application.Reservations.Cancellation;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Reservations;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Reservations.CancelReservation;

public sealed record CancelReservationCommand(Guid ReservationId) : ICommand<Unit>, IRetryOnConcurrencyConflict;

public sealed class CancelReservationValidator : AbstractValidator<CancelReservationCommand>
{
    public CancelReservationValidator() => RuleFor(c => c.ReservationId).NotEmpty();
}

/// <summary>
/// The CUSTOMER cancels their own booking (before pickup): cars back to inventory, card hold
/// released, in one transaction. Someone else's reservation is "not found" (never confirm it exists).
/// </summary>
public sealed class CancelReservationHandler(
    CurrentCustomer currentCustomer,
    IReservationRepository reservations,
    BookingRelease release,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
    : IRequestHandler<CancelReservationCommand, Unit>
{
    public async Task<Unit> Handle(CancelReservationCommand command, CancellationToken cancellationToken)
    {
        var customer = await currentCustomer.RequireAsync(cancellationToken);
        var reservation = await reservations.GetAsync(new ReservationId(command.ReservationId), cancellationToken);
        if (reservation is null || reservation.CustomerId != customer.Id)
            throw new NotFoundException("Reservation", command.ReservationId);

        var now = clock.GetUtcNow();
        reservation.Cancel(CancellationRequest.ByCustomer(customer.UserId, now));
        await release.ReleaseInventoryAsync(reservation, cancellationToken);
        await release.VoidPaymentAsync(reservation, now, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>STAFF cancel any confirmed booking (not after checkout), always with a reason the customer will see.</summary>
public sealed record AgentCancelReservationCommand(Guid ReservationId, string? Reason) : ICommand<Unit>, IRetryOnConcurrencyConflict;

public sealed class AgentCancelReservationValidator : AbstractValidator<AgentCancelReservationCommand>
{
    public AgentCancelReservationValidator() => RuleFor(c => c.ReservationId).NotEmpty();
}

public sealed class AgentCancelReservationHandler(
    ICurrentUser user,
    IReservationRepository reservations,
    BookingRelease release,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
    : IRequestHandler<AgentCancelReservationCommand, Unit>
{
    public async Task<Unit> Handle(AgentCancelReservationCommand command, CancellationToken cancellationToken)
    {
        var agentId = user.UserId ?? throw new UnauthenticatedException();
        var reservation = await reservations.GetAsync(new ReservationId(command.ReservationId), cancellationToken)
            ?? throw new NotFoundException("Reservation", command.ReservationId);

        var now = clock.GetUtcNow();
        reservation.Cancel(CancellationRequest.ByAgent(agentId, command.Reason, now));   // reason required
        await release.ReleaseInventoryAsync(reservation, cancellationToken);
        await release.VoidPaymentAsync(reservation, now, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>STAFF record a no-show (from pickup + 2h): cars back to inventory, no-show fee captured, rest of the hold released.</summary>
public sealed record MarkNoShowCommand(Guid ReservationId) : ICommand<Unit>, IRetryOnConcurrencyConflict;

public sealed class MarkNoShowValidator : AbstractValidator<MarkNoShowCommand>
{
    public MarkNoShowValidator() => RuleFor(c => c.ReservationId).NotEmpty();
}

public sealed class MarkNoShowHandler(
    ICurrentUser user,
    IReservationRepository reservations,
    BookingRelease release,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
    : IRequestHandler<MarkNoShowCommand, Unit>
{
    public async Task<Unit> Handle(MarkNoShowCommand command, CancellationToken cancellationToken)
    {
        var agentId = user.UserId ?? throw new UnauthenticatedException();
        var reservation = await reservations.GetAsync(new ReservationId(command.ReservationId), cancellationToken)
            ?? throw new NotFoundException("Reservation", command.ReservationId);

        var now = clock.GetUtcNow();
        reservation.MarkNoShow(agentId, now);   // only from pickup + 2h
        await release.ReleaseInventoryAsync(reservation, cancellationToken);
        await release.ChargeNoShowFeeAsync(reservation, now, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
