using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Common.Exceptions;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Fleet;

public sealed record ListVehiclesQuery(Guid? BranchId, VehicleCategory? Category, VehicleStatus? Status, string? Search, int Page = 1, int PageSize = 25)
    : IQuery<PagedResult<VehicleListItemDto>>;

public sealed class ListVehiclesValidator : AbstractValidator<ListVehiclesQuery>
{
    public ListVehiclesValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.Search).MaximumLength(40);
    }
}

public sealed class ListVehiclesHandler(IFleetReadModel readModel) : IRequestHandler<ListVehiclesQuery, PagedResult<VehicleListItemDto>>
{
    public Task<PagedResult<VehicleListItemDto>> Handle(ListVehiclesQuery query, CancellationToken cancellationToken) =>
        readModel.ListAsync(new VehicleFilter(query.BranchId, query.Category, query.Status, query.Search?.Trim(), query.Page, query.PageSize), cancellationToken);
}

public sealed record GetVehicleQuery(Guid VehicleId) : IQuery<VehicleDto?>;

public sealed class GetVehicleHandler(IFleetReadModel readModel) : IRequestHandler<GetVehicleQuery, VehicleDto?>
{
    public Task<VehicleDto?> Handle(GetVehicleQuery query, CancellationToken cancellationToken) => readModel.GetAsync(query.VehicleId, cancellationToken);
}

/// <summary>Timeline: registered, each rental (with its inspections and damage found), service visits, branch moves, retirement.</summary>
public sealed record GetVehicleHistoryQuery(Guid VehicleId) : IQuery<VehicleHistoryDto?>;

public sealed class GetVehicleHistoryHandler(IFleetReadModel readModel) : IRequestHandler<GetVehicleHistoryQuery, VehicleHistoryDto?>
{
    public Task<VehicleHistoryDto?> Handle(GetVehicleHistoryQuery query, CancellationToken cancellationToken) => readModel.GetHistoryAsync(query.VehicleId, cancellationToken);
}

/// <summary>Cars the agent can hand over for this booking: at the pickup branch, Available, in the category (plus upgrades on request).</summary>
public sealed record AvailableVehiclesForCheckoutQuery(Guid ReservationId, bool IncludeUpgrades = false) : IQuery<IReadOnlyList<AvailableVehicleDto>>;

public sealed class AvailableVehiclesForCheckoutHandler(ICounterReadModel counter, IFleetReadModel fleet)
    : IRequestHandler<AvailableVehiclesForCheckoutQuery, IReadOnlyList<AvailableVehicleDto>>
{
    public async Task<IReadOnlyList<AvailableVehicleDto>> Handle(AvailableVehiclesForCheckoutQuery query, CancellationToken cancellationToken)
    {
        var booking = await counter.FindAsync(query.ReservationId, cancellationToken)
            ?? throw new NotFoundException("Reservation", query.ReservationId);
        return await fleet.ListAvailableAsync(booking.PickupBranchId, Enum.Parse<VehicleCategory>(booking.Category), query.IncludeUpgrades, cancellationToken);
    }
}
