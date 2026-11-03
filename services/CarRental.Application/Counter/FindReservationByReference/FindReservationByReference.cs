using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.ReadModels;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Counter.FindReservationByReference;

/// <summary>
/// Counter lookup: an agent may open ANY customer's booking by its reference (ROAM-XXXX-XXXX-XXXX-XXXX)
/// or id. Customers still only ever see their own (GetReservationById filters by owner).
/// </summary>
public sealed record FindReservationByReferenceQuery(string Reference) : IQuery<CounterBookingDto?>;

public sealed class FindReservationByReferenceValidator : AbstractValidator<FindReservationByReferenceQuery>
{
    public FindReservationByReferenceValidator() =>
        RuleFor(q => q.Reference)
            .Must(r => BookingReference.TryParse(r, out _, out _))
            .WithMessage("Enter a booking reference like ROAM-8F4B-92A1-0C7E-449D.");
}

public sealed class FindReservationByReferenceHandler(ICounterReadModel readModel, TimeProvider clock)
    : IRequestHandler<FindReservationByReferenceQuery, CounterBookingDto?>
{
    public async Task<CounterBookingDto?> Handle(FindReservationByReferenceQuery query, CancellationToken cancellationToken)
    {
        if (!BookingReference.TryParse(query.Reference, out var id, out var prefix))
            return null;   // the validator already rejected this shape; nothing can match it
        var booking = id != Guid.Empty
            ? await readModel.FindAsync(id, cancellationToken)
            : await readModel.FindByReferencePrefixAsync(prefix, cancellationToken);
        return booking?.WithLateFlag(clock.GetUtcNow());
    }
}
