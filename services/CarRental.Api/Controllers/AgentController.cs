using CarRental.Application.Counter;
using CarRental.Application.Counter.AgentRegisterCustomer;
using CarRental.Application.Counter.FindReservationByReference;
using CarRental.Application.Counter.GetBranchPickups;
using CarRental.Application.Counter.GetBranchReturns;
using CarRental.Application.Inspections;
using CarRental.Application.Payments;
using CarRental.Application.Payments.TakeCounterPayment;
using CarRental.Application.Reservations.CancelReservation;
using CarRental.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Api.Controllers;

/// <summary>
/// The staff ("Roam Staff") counter app's queries and commands. Every action requires the Agent role:
/// a customer token gets 403, an anonymous call 401.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Agent)]
[Route("api/agent")]
[Produces("application/json")]
public sealed class AgentController(ISender sender) : ControllerBase
{
    public sealed record CancelRequest(string? Reason);
    public sealed record CounterPaymentRequest(string PaymentToken);

    /// <summary>Cancel any confirmed booking (not after checkout). A reason is required: the customer sees it.</summary>
    [HttpPost("reservations/{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, CancelRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new AgentCancelReservationCommand(id, request.Reason), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Pay at the branch: hold the booking total on the customer's card at the counter (token from the
    /// gateway's hosted fields, never a card number). Required before the keys are handed over.
    /// </summary>
    [HttpPost("reservations/{id:guid}/payment")]
    [ProducesResponseType<PaymentSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> TakePayment(Guid id, CounterPaymentRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new TakeCounterPaymentCommand(id, request.PaymentToken), cancellationToken));

    /// <summary>Record a no-show (from pickup + 2h): no-show fee captured, rest of the hold released.</summary>
    [HttpPost("reservations/{id:guid}/no-show")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> NoShow(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new MarkNoShowCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Bookings collecting a car at the branch on the date (SAST), confirmed or already checked out.</summary>
    /// <example>GET /api/agent/branches/{branchId}/pickups?date=2026-12-01</example>
    [HttpGet("branches/{branchId:guid}/pickups")]
    [ProducesResponseType<IReadOnlyList<CounterBookingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Pickups(Guid branchId, [FromQuery] DateOnly date, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetBranchPickupsQuery(branchId, date), cancellationToken));

    /// <summary>Cars still out and due back at the branch by the end of the date (overdue ones flagged isLate).</summary>
    [HttpGet("branches/{branchId:guid}/returns")]
    [ProducesResponseType<IReadOnlyList<CounterBookingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Returns(Guid branchId, [FromQuery] DateOnly date, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetBranchReturnsQuery(branchId, date), cancellationToken));

    /// <summary>Any customer's booking by reference (ROAM-XXXX-XXXX-XXXX-XXXX) or id. 404 when not found.</summary>
    [HttpGet("reservations/{reference}")]
    [ProducesResponseType<CounterBookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindReservation(string reference, CancellationToken cancellationToken)
    {
        var booking = await sender.Send(new FindReservationByReferenceQuery(reference), cancellationToken);
        return booking is null ? NotFound() : Ok(booking);
    }

    /// <summary>
    /// Register a walk-in customer: account + driver profile. Returns the one-time temporary password
    /// ONCE (it is never logged or stored in clear). 422 with a code if the ID or dates are invalid,
    /// account.email_in_use if the email already has an account.
    /// </summary>
    [HttpPost("customers")]
    [ProducesResponseType<AgentRegisteredCustomerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegisterCustomer(AgentRegisterCustomerCommand command, CancellationToken cancellationToken)
    {
        var created = await sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }
}
