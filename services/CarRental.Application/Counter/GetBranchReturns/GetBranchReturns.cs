using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.ReadModels;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Counter.GetBranchReturns;

/// <summary>
/// Counter: cars still out that are due back at this branch by the end of this day (SAST), including
/// ones that were due earlier and are now overdue. Late ones are flagged with the Domain's rule.
/// </summary>
public sealed record GetBranchReturnsQuery(Guid BranchId, DateOnly Date) : IQuery<IReadOnlyList<CounterBookingDto>>;

public sealed class GetBranchReturnsValidator : AbstractValidator<GetBranchReturnsQuery>
{
    public GetBranchReturnsValidator() => RuleFor(q => q.BranchId).NotEmpty();
}

public sealed class GetBranchReturnsHandler(ICounterReadModel readModel, TimeProvider clock)
    : IRequestHandler<GetBranchReturnsQuery, IReadOnlyList<CounterBookingDto>>
{
    public async Task<IReadOnlyList<CounterBookingDto>> Handle(GetBranchReturnsQuery query, CancellationToken cancellationToken)
    {
        var (_, endOfDay) = BranchTime.Day(query.Date);
        var now = clock.GetUtcNow();
        var rows = await readModel.ListOpenReturnsAsync(query.BranchId, endOfDay, cancellationToken);
        return rows.Select(r => r.WithLateFlag(now)).ToList();
    }
}
