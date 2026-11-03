using CarRental.Application.Inspections;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;

namespace CarRental.Application.Fleet;

public sealed record VehicleListItemDto(
    Guid Id,
    string Registration,
    string Make,
    string Model,
    int Year,
    string Category,
    string Transmission,
    string Status,
    Guid CurrentBranchId,
    string CurrentBranchCode,
    string CurrentBranchName,
    int OdometerKm);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record VehicleDto(
    Guid Id,
    string Registration,
    string Vin,
    string Make,
    string Model,
    int Year,
    string Category,
    string Transmission,
    string Status,
    Guid HomeBranchId,
    string HomeBranchName,
    Guid CurrentBranchId,
    string CurrentBranchName,
    int OdometerKm,
    DateTimeOffset CreatedAt,
    Guid? CurrentRentalId,
    Guid? CurrentReservationId);

/// <summary>One line of the vehicle history timeline. <see cref="Rental"/> is set for kind "Rental".</summary>
public sealed record VehicleHistoryEventDto(
    string Kind,
    DateTimeOffset At,
    int? OdometerKm,
    string? BranchName,
    string? Note,
    VehicleRentalHistoryDto? Rental);

public sealed record VehicleRentalHistoryDto(
    Guid RentalId,
    Guid ReservationId,
    string Reference,
    string CustomerName,
    DateTimeOffset PickedUpAt,
    DateTimeOffset? ReturnedAt,
    int OdometerOut,
    int? OdometerIn,
    int? DistanceKm,
    decimal LateFee,
    string Currency,
    IReadOnlyList<InspectionSummaryDto> Inspections);

public sealed record VehicleHistoryDto(VehicleDto Vehicle, IReadOnlyList<VehicleHistoryEventDto> Timeline);

public sealed record AvailableVehicleDto(
    Guid Id,
    string Registration,
    string Make,
    string Model,
    int Year,
    string Category,
    string Transmission,
    int OdometerKm,
    bool IsUpgrade);

public sealed record VehicleFilter(Guid? BranchId, VehicleCategory? Category, VehicleStatus? Status, string? Search, int Page, int PageSize);
