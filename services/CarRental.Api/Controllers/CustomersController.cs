using CarRental.Application.Customers.GetMyProfile;
using CarRental.Application.Customers.SaveMyProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Api.Controllers;

/// <summary>The signed-in customer's own driver profile. There is no "get customer by id": only "me".</summary>
[ApiController]
[Authorize]
[Route("api/customers/me")]
public sealed class CustomersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CustomerProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var profile = await sender.Send(new GetMyProfileQuery(), cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>Create or update. 422 with a code if the ID number is invalid or doesn't match the date of birth.</summary>
    [HttpPut]
    [ProducesResponseType<CustomerProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Save(SaveMyProfileCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return Ok(await sender.Send(new GetMyProfileQuery(), cancellationToken));
    }
}
