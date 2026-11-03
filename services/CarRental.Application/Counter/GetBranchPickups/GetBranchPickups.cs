using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.ReadModels;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Counter.GetBranchPickups;

/// <summary>Counter: bookings collecting a car at this branch on this day (SAST). Agents only (enforced by the API).</summary>
public sealed record GetBranchPickupsQuery(Guid BranchId, DateOnly Date) : IQuery<IReadOnlyList<CounterBookingDto>>;

public sealed class GetBranchPickupsValidator : AbstractValidator<GetBranchPickupsQuery>
{
    public GetBranchPickupsValidator() => RuleFor(q => q.BranchId).NotEmpty();
}

public sealed class GetBranchPickupsHandler(ICounterReadModel readModel)
    : IRequestHandler<GetBranchPickupsQuery, IReadOnlyList<CounterBookingDto>>
{
    public Task<IReadOnlyList<CounterBookingDto>> Handle(GetBranchPickupsQuery query, CancellationToken cancellationToken)
    {
        var (from, to) = BranchTime.Day(query.Date);
        return readModel.ListPickupsAsync(query.BranchId, from, to, cancellationToken);
    }
}
