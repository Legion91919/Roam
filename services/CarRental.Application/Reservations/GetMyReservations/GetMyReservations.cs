using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Common.Exceptions;
using CarRental.Application.Reservations.GetReservationById;
using MediatR;

namespace CarRental.Application.Reservations.GetMyReservations;

public sealed record GetMyReservationsQuery : IQuery<IReadOnlyList<ReservationDetailsDto>>;

public sealed class GetMyReservationsHandler(ICurrentUser user, IReservationReadModel readModel)
    : IRequestHandler<GetMyReservationsQuery, IReadOnlyList<ReservationDetailsDto>>
{
    public Task<IReadOnlyList<ReservationDetailsDto>> Handle(GetMyReservationsQuery query, CancellationToken cancellationToken) =>
        readModel.ListForUserAsync(user.UserId ?? throw new UnauthenticatedException(), cancellationToken);
}
