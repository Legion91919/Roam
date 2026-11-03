using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Common.Exceptions;
using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Fleet;

// ---------------------------------------------------------------------------------------------
// FLEET COMMANDS (Agent role, enforced by the API). Each loads/creates the Vehicle aggregate,
// calls one method and saves once. Inventory follows through pre-commit domain event handlers
// (FleetCapacityHandlers), in the SAME transaction.
// ---------------------------------------------------------------------------------------------

public sealed record RegisterVehicleCommand(
    string Registration,
    string Vin,
    string Make,
    string Model,
    int Year,
    VehicleCategory Category,
    Transmission Transmission,
    Guid HomeBranchId,
    int OdometerKm) : ICommand<Guid>;

public sealed class RegisterVehicleValidator : AbstractValidator<RegisterVehicleCommand>
{
    public RegisterVehicleValidator()
    {
        RuleFor(c => c.Registration).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Vin).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Make).NotEmpty().MaximumLength(40);
        RuleFor(c => c.Model).NotEmpty().MaximumLength(40);
        RuleFor(c => c.Category).IsInEnum();
        RuleFor(c => c.Transmission).IsInEnum();
        RuleFor(c => c.HomeBranchId).NotEmpty();
        RuleFor(c => c.OdometerKm).GreaterThanOrEqualTo(0);
    }
}

public sealed class RegisterVehicleHandler(IVehicleRepository vehicles, IBranchRepository branches, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<RegisterVehicleCommand, Guid>
{
    public async Task<Guid> Handle(RegisterVehicleCommand command, CancellationToken cancellationToken)
    {
        var registration = Registration.Create(command.Registration);
        var vin = Vin.Create(command.Vin);
        var branch = new BranchId(command.HomeBranchId);

        if (!await branches.ExistsAsync(branch, cancellationToken))
            throw new DomainException("branch.not_found", "That branch does not exist.");
        if (await vehicles.RegistrationInUseAsync(registration, cancellationToken))
            throw new DomainException("vehicle.duplicate_registration", $"{registration} is already in the fleet.");
        if (await vehicles.VinInUseAsync(vin, cancellationToken))
            throw new DomainException("vehicle.duplicate_vin", $"VIN {vin} is already in the fleet.");

        var vehicle = Vehicle.Register(registration, vin, command.Make, command.Model, command.Year,
            command.Category, command.Transmission, branch, command.OdometerKm, clock.GetUtcNow());
        vehicles.Add(vehicle);

        await unitOfWork.SaveChangesAsync(cancellationToken);   // + inventory capacity, same transaction
        return vehicle.Id.Value;
    }
}

public sealed record SendVehicleToServiceCommand(Guid VehicleId, string Reason) : ICommand<Unit>;

public sealed class SendVehicleToServiceHandler(IVehicleRepository vehicles, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<SendVehicleToServiceCommand, Unit>
{
    public async Task<Unit> Handle(SendVehicleToServiceCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await FleetLookup.RequireAsync(vehicles, command.VehicleId, cancellationToken);
        vehicle.SendToService(command.Reason, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record ReturnVehicleFromServiceCommand(Guid VehicleId, int OdometerKm) : ICommand<Unit>;

public sealed class ReturnVehicleFromServiceHandler(IVehicleRepository vehicles, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<ReturnVehicleFromServiceCommand, Unit>
{
    public async Task<Unit> Handle(ReturnVehicleFromServiceCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await FleetLookup.RequireAsync(vehicles, command.VehicleId, cancellationToken);
        vehicle.ReturnFromService(command.OdometerKm, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Retiring removes one car from future inventory in the same transaction (fails if those days are fully booked).</summary>
public sealed record RetireVehicleCommand(Guid VehicleId, string Reason) : ICommand<Unit>, IRetryOnConcurrencyConflict;

public sealed class RetireVehicleHandler(IVehicleRepository vehicles, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<RetireVehicleCommand, Unit>
{
    public async Task<Unit> Handle(RetireVehicleCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await FleetLookup.RequireAsync(vehicles, command.VehicleId, cancellationToken);
        vehicle.Retire(command.Reason, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

internal static class FleetLookup
{
    public static async Task<Vehicle> RequireAsync(IVehicleRepository vehicles, Guid id, CancellationToken cancellationToken) =>
        await vehicles.GetAsync(new VehicleId(id), cancellationToken) ?? throw new NotFoundException("Vehicle", id);
}
