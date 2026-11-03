namespace CarRental.Application.Inspections;

/// <summary>
/// An inspection report for display. Shown to staff, and (once Completed) to the booking's owner.
/// No user ids and no storage keys: only the inspector's first name and file metadata.
/// </summary>
public sealed record InspectionDto(
    Guid Id,
    Guid ReservationId,
    Guid? RentalId,
    Guid VehicleId,
    string VehicleRegistration,
    string Kind,
    string Status,
    string InspectedByName,
    DateTimeOffset StartedAt,
    DateTimeOffset? InspectedAt,
    int? OdometerKm,
    int? FuelEighths,
    IReadOnlyList<ChecklistItemDto> Checklist,
    IReadOnlyList<DamageMarkDto> Damage,
    IReadOnlyList<AttachmentDto> Attachments,
    IReadOnlyList<NewDamageDto> NewDamage,
    DateTimeOffset? CustomerAcknowledgedAt);

public sealed record ChecklistItemDto(string Code, string Status, string? Note);

public sealed record DamageMarkDto(string Panel, string Description, string Severity);

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string Angle, DateTimeOffset UploadedAt);

public sealed record NewDamageDto(string? Item, string Panel, string Description, string? Severity);

/// <summary>Short form for lists (vehicle history, counter booking card, "report available").</summary>
public sealed record InspectionSummaryDto(Guid Id, string Kind, string Status, DateTimeOffset? InspectedAt, int DamagedItems, IReadOnlyList<string> NewDamage);
