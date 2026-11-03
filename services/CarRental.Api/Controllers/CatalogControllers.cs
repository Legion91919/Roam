using CarRental.Application.Availability.SearchAvailability;
using CarRental.Application.Branches.GetBranches;
using CarRental.Application.Pricing;
using CarRental.Application.Pricing.GetQuote;
using CarRental.Application.Abstractions.Terms;
using CarRental.Application.Terms.GetCurrentTerms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/branches")]
public sealed class BranchesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BranchDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetBranchesQuery(), cancellationToken));
}

[ApiController]
[Route("api/availability")]
public sealed class AvailabilityController(ISender sender) : ControllerBase
{
    /// <example>GET /api/availability?branchId=...&amp;pickupAt=2026-12-01T09:00:00%2B02:00&amp;returnAt=2026-12-04T09:00:00%2B02:00</example>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryAvailabilityDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] Guid branchId, [FromQuery] DateTimeOffset pickupAt, [FromQuery] DateTimeOffset returnAt, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SearchAvailabilityQuery(branchId, pickupAt, returnAt), cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/quotes")]
public sealed class QuotesController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Price a trip for the signed-in customer (age, licence and loyalty tier come from their profile).
    /// POST because the input is a rich object, but nothing is stored.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<QuoteDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Quote(GetQuoteQuery query, CancellationToken cancellationToken) =>
        Ok(await sender.Send(query, cancellationToken));
}

[ApiController]
[Route("api/terms")]
public sealed class TermsController(ISender sender) : ControllerBase
{
    /// <summary>The current rental terms. Bookings must send back <c>version</c> as acceptedTermsVersion.</summary>
    [HttpGet("current")]
    [ProducesResponseType<TermsDocument>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Current(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetCurrentTermsQuery(), cancellationToken));
}
