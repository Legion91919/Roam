using CarRental.Application.Fleet;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;
using CarRental.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Api.Controllers;

/// <summary>The fleet (Agent role): capture cars, change their status, see their history.</summary>
[ApiController]
[Authorize(Roles = Roles.Agent)]
[Route("api/agent/vehicles")]
[Produces("application/json")]
public sealed class FleetController(ISender sender) : ControllerBase
{
    public sealed record ReasonRequest(string Reason);
    public sealed record OdometerRequest(int OdometerKm);

    /// <example>GET /api/agent/vehicles?branchId=…&amp;category=Suv&amp;status=Available&amp;search=CA&amp;page=1&amp;pageSize=25</example>
    [HttpGet]
    [ProducesResponseType<PagedResult<VehicleListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? branchId, [FromQuery] VehicleCategory? category, [FromQuery] VehicleStatus? status,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new ListVehiclesQuery(branchId, category, status, search, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        await sender.Send(new GetVehicleQuery(id), cancellationToken) is { } vehicle ? Ok(vehicle) : NotFound();

    /// <summary>Timeline: registered, rentals (with inspections and damage), service visits, branch moves, retirement.</summary>
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType<VehicleHistoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken) =>
        await sender.Send(new GetVehicleHistoryQuery(id), cancellationToken) is { } history ? Ok(history) : NotFound();

    /// <summary>Cars that can be handed over for this booking (at the branch, Available, in category; upgrades on request).</summary>
    [HttpGet("available-for/{reservationId:guid}")]
    [ProducesResponseType<IReadOnlyList<AvailableVehicleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AvailableFor(Guid reservationId, [FromQuery] bool includeUpgrades, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new AvailableVehiclesForCheckoutQuery(reservationId, includeUpgrades), cancellationToken));

    /// <summary>Register a car: 201; 422 vehicle.invalid_registration / invalid_vin / too_old / duplicate_*.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(RegisterVehicleCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, await sender.Send(new GetVehicleQuery(id), cancellationToken));
    }

    [HttpPost("{id:guid}/service")]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SendToService(Guid id, ReasonRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SendVehicleToServiceCommand(id, request.Reason), cancellationToken);
        return Ok(await sender.Send(new GetVehicleQuery(id), cancellationToken));
    }

    [HttpPost("{id:guid}/service/return")]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReturnFromService(Guid id, OdometerRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new ReturnVehicleFromServiceCommand(id, request.OdometerKm), cancellationToken);
        return Ok(await sender.Send(new GetVehicleQuery(id), cancellationToken));
    }

    /// <summary>Retire: 422 vehicle.on_rent, or inventory.capacity_below_reserved if its cars are already promised.</summary>
    [HttpPost("{id:guid}/retire")]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Retire(Guid id, ReasonRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new RetireVehicleCommand(id, request.Reason), cancellationToken);
        return Ok(await sender.Send(new GetVehicleQuery(id), cancellationToken));
    }
}
