using CarRental.Application.Rentals.CheckOutVehicle;
using CarRental.Application.Rentals.ReturnVehicle;
using CarRental.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Api.Controllers;

/// <summary>Counter operations: hand over the keys, take the car back. Staff only (Agent role).</summary>
[ApiController]
[Authorize(Roles = Roles.Agent)]
[Route("api/rentals")]
public sealed class RentalsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Hand over the keys of a fleet car: { reservationId, vehicleId, freeUpgrade }. Needs a completed
    /// checkout inspection of that car (422 rental.inspection_required); its odometer is the start reading.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CheckOut(CheckOutVehicleCommand command, CancellationToken cancellationToken)
    {
        var rentalId = await sender.Send(command, cancellationToken);
        return Created($"/api/rentals/{rentalId}", new { rentalId });
    }

    /// <summary>Close the rental after the completed return inspection (its odometer is the end reading); captures the payment.</summary>
    [HttpPost("{id:guid}/return")]
    [ProducesResponseType<ReturnVehicleResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Return(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ReturnVehicleCommand(id), cancellationToken));
}
