using CarRental.Domain.Common;

namespace CarRental.Domain.Inspections;

public enum InspectionKind
{
    Checkout = 0,
    Return = 1
}

public enum InspectionStatus
{
    Draft = 0,
    Completed = 1
}

/// <summary>The fixed checklist every inspection answers, in display order.</summary>
public enum ChecklistItemCode
{
    ExteriorFront = 0,
    ExteriorRear = 1,
    ExteriorLeft = 2,
    ExteriorRight = 3,
    Roof = 4,
    Windscreen = 5,
    WheelsAndTyres = 6,
    SpareWheelAndJack = 7,
    InteriorAndSeats = 8,
    WarningLights = 9,
    LicenceDiscValid = 10,
    DriverIdAndLicenceVerified = 11
}

public enum ChecklistItemStatus
{
    Unanswered = 0,
    Ok = 1,
    Damaged = 2,
    Missing = 3,
    NotApplicable = 4
}

public enum DamageSeverity
{
    Minor = 0,
    Moderate = 1,
    Severe = 2
}

/// <summary>Which view a photo shows. A checkout report needs Front, Back, Left and Right.</summary>
public enum AttachmentAngle
{
    Front = 0,
    Back = 1,
    Left = 2,
    Right = 3,
    Interior = 4,
    Damage = 5,
    Document = 6,
    Other = 7
}

/// <summary>VALUE OBJECT: one answered line of the checklist. Damaged or Missing needs a note to complete.</summary>
public sealed record ChecklistItem(ChecklistItemCode Code, ChecklistItemStatus Status, string? Note)
{
    public bool NeedsNote => Status is ChecklistItemStatus.Damaged or ChecklistItemStatus.Missing;
    public bool HasDamage => Status is ChecklistItemStatus.Damaged or ChecklistItemStatus.Missing;
}

/// <summary>VALUE OBJECT: fuel gauge in eighths, 0 (empty) to 8 (full).</summary>
public sealed record FuelLevel
{
    public int Eighths { get; private init; }

    public FuelLevel(int eighths)
    {
        if (eighths is < 0 or > 8)
            throw new DomainException("inspection.invalid_fuel", "Fuel level is in eighths: 0 (empty) to 8 (full).");
        Eighths = eighths;
    }

    public static FuelLevel Full => new(8);
}

/// <summary>VALUE OBJECT: a damage mark on a panel ("Rear bumper", "Scratch 10 cm", Minor).</summary>
public sealed record DamageMark(string Panel, string Description, DamageSeverity Severity)
{
    public static DamageMark Create(string? panel, string? description, DamageSeverity severity)
    {
        if (string.IsNullOrWhiteSpace(panel) || panel.Trim().Length > 60)
            throw new DomainException("inspection.damage_panel_required", "Each damage mark needs a panel (max 60 characters).");
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 300)
            throw new DomainException("inspection.damage_description_required", "Each damage mark needs a description (max 300 characters).");
        return new DamageMark(panel.Trim(), description.Trim(), severity);
    }

    /// <summary>Same panel, case-insensitive: how a return report recognises damage already seen at checkout.</summary>
    public bool SamePanel(DamageMark other) => string.Equals(Panel, other.Panel, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// VALUE OBJECT: metadata of an uploaded photo or file. The bytes live in file storage under
/// <see cref="StorageKey"/> (random, never the user's file name); the Domain never touches them.
/// The identity is named <c>AttachmentId</c>, not <c>Id</c>: it is the attachment's business identity
/// (used in download URLs), not a table key, and the attachments are stored as a JSON column with their report.
/// </summary>
public sealed record InspectionAttachment(
    Identifiers.AttachmentId AttachmentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StorageKey,
    AttachmentAngle Angle,
    DateTimeOffset UploadedAt,
    string UploadedByUserId);

/// <summary>What the return report found that the checkout report did not have.</summary>
public sealed record NewDamage(ChecklistItemCode? Item, string Panel, string Description, DamageSeverity? Severity);
