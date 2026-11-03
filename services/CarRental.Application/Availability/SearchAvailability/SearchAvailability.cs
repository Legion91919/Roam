using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.ReadModels;
using CarRental.Domain.Pricing;
using CarRental.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Availability.SearchAvailability;

/// <summary>"Which categories can I still book at this branch for these dates, and from what price?"</summary>
public sealed record SearchAvailabilityQuery(Guid BranchId, DateTimeOffset PickupAt, DateTimeOffset ReturnAt)
    : IQuery<IReadOnlyList<CategoryAvailabilityDto>>;

public sealed record CategoryAvailabilityDto(string Category, int AvailableCars, decimal DailyRate, string Currency);

public sealed class SearchAvailabilityValidator : AbstractValidator<SearchAvailabilityQuery>
{
    public SearchAvailabilityValidator()
    {
        RuleFor(q => q.BranchId).NotEmpty();
        RuleFor(q => q.ReturnAt).GreaterThan(q => q.PickupAt);
    }
}

/// <summary>
/// Reads per-day availability from whichever read model is configured (SQL or Mongo) and reduces
/// it to "cars available for the WHOLE period" = the minimum over the occupied days.
/// A category with a missing day is not offered for that period.
/// </summary>
public sealed class SearchAvailabilityHandler(IAvailabilityReadModel readModel, RateCard rates)
    : IRequestHandler<SearchAvailabilityQuery, IReadOnlyList<CategoryAvailabilityDto>>
{
    public async Task<IReadOnlyList<CategoryAvailabilityDto>> Handle(SearchAvailabilityQuery query, CancellationToken cancellationToken)
    {
        var period = RentalPeriod.Create(query.PickupAt, query.ReturnAt);
        var requiredDays = period.OccupiedDates().Count();

        var rows = await readModel.GetDailyAvailabilityAsync(query.BranchId, period.FirstDay, period.LastDay, cancellationToken);

        return rows
            .GroupBy(r => r.Category)
            .Where(g => g.Select(r => r.Date).Distinct().Count() == requiredDays)
            .Select(g =>
            {
                var rate = rates.DailyRate(g.Key);
                return new CategoryAvailabilityDto(g.Key.ToString(), Math.Max(0, g.Min(r => r.Available)), rate.Amount, rate.Currency);
            })
            .OrderBy(dto => dto.DailyRate)
            .ToList();
    }
}
