using CarRental.Api.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Api.Controllers;

/// <summary>
/// Mock "hosted fields": card details in, single-use token out. Deliberately NOT a MediatR request,
/// so card data never enters the Application pipeline (which logs request names and failures).
/// </summary>
[ApiController]
[Authorize]
[Route("api/payments")]
public sealed class PaymentsController(TimeProvider clock) : ControllerBase
{
    /// <summary>Validate a test card (Luhn + expiry) and return { token, brand, last4 }. Nothing is stored or logged.</summary>
    [HttpPost("tokenize")]
    [ProducesResponseType<MockHostedFields.CardToken>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public IActionResult Tokenize(MockHostedFields.CardTokenRequest request)
    {
        var (token, errors) = MockHostedFields.Tokenize(request, clock.GetUtcNow());
        if (token is not null)
            return Ok(token);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Card details are invalid",
            Detail = "Check the card number, expiry and CVC.",
        };
        problem.Extensions["code"] = "payment.invalid_card";
        problem.Extensions["errors"] = errors;
        return BadRequest(problem);
    }
}
