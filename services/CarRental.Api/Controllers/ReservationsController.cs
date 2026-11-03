using CarRental.Application.Inspections;
using CarRental.Application.Reservations.CancelReservation;
using CarRental.Application.Reservations.CreateReservation;
using CarRental.Application.Reservations.GetMyReservations;
using CarRental.Application.Reservations.GetReservationById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Api.Controllers;

/// <summary>
/// THIN CONTROLLER: translate HTTP to a MediatR request and back. No business logic, no
/// repositories, no DbContext. SOLID (S): the controller's one job is HTTP.
/// The commands double as request bodies to keep the PoC small; a public API would add separate
/// versioned request contracts so the HTTP shape can evolve independently of the use cases.
/// </summary>
[ApiController]
[Authorize]
[Route("api/reservations")]
[Produces("application/json")]
public sealed class ReservationsController(ISender sender) : ControllerBase
{
    /// <summary>Book a car. 201 with the price breakdown, 409 if sold out, 422 if a business rule fails.</summary>
    [HttpPost]
    [ProducesResponseType<CreateReservationResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateReservationCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.ReservationId }, result);
    }

    /// <summary>The signed-in customer's reservations, newest pickup first.</summary>
    [HttpGet("mine")]
    [ProducesResponseType<IReadOnlyList<ReservationDetailsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMyReservationsQuery(), cancellationToken));

    /// <summary>404 if it doesn't exist OR belongs to someone else (never confirm others' bookings exist).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReservationDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var reservation = await sender.Send(new GetReservationByIdQuery(id), cancellationToken);
        return reservation is null ? NotFound() : Ok(reservation);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new CancelReservationCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>The COMPLETED inspection reports (checkout, return) of your own booking. 404 if it isn't yours.</summary>
    [HttpGet("{id:guid}/inspections")]
    [ProducesResponseType<IReadOnlyList<InspectionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Inspections(Guid id, CancellationToken cancellationToken)
    {
        if (await sender.Send(new GetReservationByIdQuery(id), cancellationToken) is null)
            return NotFound();
        return Ok(await sender.Send(new GetMyReservationInspectionsQuery(id), cancellationToken));
    }

    /// <summary>"I've reviewed this report": stores when you confirmed it. 404 if it isn't yours or not completed.</summary>
    [HttpPost("{id:guid}/inspections/{inspectionId:guid}/acknowledge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Acknowledge(Guid id, Guid inspectionId, CancellationToken cancellationToken)
    {
        await sender.Send(new AcknowledgeInspectionCommand(id, inspectionId), cancellationToken);
        return NoContent();
    }
}
