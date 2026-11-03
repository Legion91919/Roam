using CarRental.Application.Account.GetMyAccount;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Api.Controllers;

/// <summary>The signed-in user's account: email and roles, from the token. Lets the client route staff to the staff app.</summary>
[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController(ISender sender) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMyAccountQuery(), cancellationToken));
}
