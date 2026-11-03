using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Common.Exceptions;
using CarRental.Application.Payments;
using MediatR;

namespace CarRental.Application.Reservations.GetReservationById;

/// <summary>
/// QUERY side of CQRS-lite: no aggregate, no domain logic, no change tracking.
/// The read model returns a flat DTO shaped for the screen, filtered to the signed-in owner.
/// </summary>
public sealed record GetReservationByIdQuery(Guid ReservationId) : IQuery<ReservationDetailsDto?>;

public sealed record ReservationDetailsDto(
    Guid Id,
    string Category,
    string Status,
    Guid PickupBranchId,
    string PickupBranchName,
    Guid ReturnBranchId,
    string ReturnBranchName,
    DateTimeOffset PickupAt,
    DateTimeOffset ReturnAt,
    decimal Total,
    string Currency,
    string DriverName,
    string TermsVersion,
    DateTimeOffset CreatedAt,
    PaymentSummaryDto? Payment,
    CancellationDto? Cancellation,
    bool HasInspectionReport,
    string PaymentOption = "Card");

/// <summary>
/// Who ended the booking and why, as the CUSTOMER sees it: "Customer" (you) or "Agent" (Roam) plus
/// the agent's reason. Never the agent's user id.
/// </summary>
public sealed record CancellationDto(string CancelledBy, string? Reason, DateTimeOffset At, bool NoShow);

public sealed class GetReservationByIdHandler(ICurrentUser user, IReservationReadModel readModel)
    : IRequestHandler<GetReservationByIdQuery, ReservationDetailsDto?>
{
    public Task<ReservationDetailsDto?> Handle(GetReservationByIdQuery query, CancellationToken cancellationToken) =>
        readModel.GetForUserAsync(query.ReservationId, user.UserId ?? throw new UnauthenticatedException(), cancellationToken);
}
