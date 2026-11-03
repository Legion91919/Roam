using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Common.Customers;
using CarRental.Domain.Eligibility;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Pricing;
using CarRental.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Pricing.GetQuote;

/// <summary>
/// Price a trip WITHOUT booking it (the review screen). It is a query because nothing is saved,
/// yet it uses the domain pricer and eligibility policy with the signed-in customer's VERIFIED
/// profile, so the quote shown is exactly the price CreateReservation will charge.
/// </summary>
public sealed record GetQuoteQuery(
    VehicleCategory Category,
    Guid PickupBranchId,
    Guid ReturnBranchId,
    DateTimeOffset PickupAt,
    DateTimeOffset ReturnAt,
    Extras Extras = Extras.None) : IQuery<QuoteDto>;

public sealed class GetQuoteValidator : AbstractValidator<GetQuoteQuery>
{
    public GetQuoteValidator()
    {
        RuleFor(q => q.PickupBranchId).NotEmpty();
        RuleFor(q => q.ReturnBranchId).NotEmpty();
        RuleFor(q => q.Category).IsInEnum();
        RuleFor(q => q.ReturnAt).GreaterThan(q => q.PickupAt);
    }
}

public sealed class GetQuoteHandler(CurrentCustomer currentCustomer, RentalPricer pricer) : IRequestHandler<GetQuoteQuery, QuoteDto>
{
    public async Task<QuoteDto> Handle(GetQuoteQuery query, CancellationToken cancellationToken)
    {
        var driver = (await currentCustomer.RequireAsync(cancellationToken)).ToDriverProfile();
        var period = RentalPeriod.Create(query.PickupAt, query.ReturnAt);
        var request = new PricingRequest(query.Category, period, driver, new BranchId(query.PickupBranchId), new BranchId(query.ReturnBranchId), query.Extras);

        DriverEligibilityPolicy.EnsureEligible(driver, query.Category, request.PickupDate);

        return QuoteDto.From(pricer.Quote(request));
    }
}
