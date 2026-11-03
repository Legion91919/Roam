using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;

namespace CarRental.Domain.Inspections;

/// <summary>
/// AGGREGATE ROOT: the condition report an agent completes when the keys go out (Checkout) and when
/// they come back (Return). Inspections are EVIDENCE: once completed, nothing can change.
/// </summary>
/// <remarks>
/// Invariants:
///  * Edits, uploads and removals only while Draft (inspection.locked).
///  * To complete: every checklist item answered (inspection.incomplete); Damaged/Missing items have a
///    note (inspection.note_required); an odometer reading and fuel level are recorded.
///  * A Checkout report also needs photos of the front, back, left and right
///    (inspection.photos_required) and "driver ID and licence verified" = Ok (inspection.driver_not_verified).
///  * At most 20 attachments.
///  * One Checkout and one Return report per reservation (unique index + use case check).
///  * A Return report lists NEW damage: damaged/missing items or damage marks not on the checkout report.
/// </remarks>
public sealed class InspectionReport : AggregateRoot<InspectionId>
{
    public const int MaxAttachments = 20;
    public const int MaxNoteLength = 300;

    public static readonly AttachmentAngle[] RequiredCheckoutAngles =
        [AttachmentAngle.Front, AttachmentAngle.Back, AttachmentAngle.Left, AttachmentAngle.Right];

    private List<ChecklistItem> _checklist = new();
    private List<DamageMark> _damage = new();
    private List<InspectionAttachment> _attachments = new();
    private List<NewDamage> _newDamage = new();

    public ReservationId ReservationId { get; private set; }
    public RentalId? RentalId { get; private set; }
    public VehicleId VehicleId { get; private set; }
    public InspectionKind Kind { get; private set; }
    public InspectionStatus Status { get; private set; }
    public string InspectedByUserId { get; private set; } = string.Empty;
    public string InspectedByName { get; private set; } = string.Empty;
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? InspectedAt { get; private set; }
    public int? OdometerKm { get; private set; }
    public FuelLevel? Fuel { get; private set; }
    public DateTimeOffset? CustomerAcknowledgedAt { get; private set; }

    public IReadOnlyList<ChecklistItem> Checklist => _checklist.AsReadOnly();
    public IReadOnlyList<DamageMark> Damage => _damage.AsReadOnly();
    public IReadOnlyList<InspectionAttachment> Attachments => _attachments.AsReadOnly();
    public IReadOnlyList<NewDamage> NewDamage => _newDamage.AsReadOnly();
    public bool CustomerAcknowledged => CustomerAcknowledgedAt is not null;

    private InspectionReport() { }   // EF Core

    /// <param name="inspectorName">First name only is shown to the customer.</param>
    public static InspectionReport Start(
        ReservationId reservationId, VehicleId vehicleId, RentalId? rentalId, InspectionKind kind,
        string inspectorUserId, string inspectorName, DateTimeOffset now)
    {
        if (kind == InspectionKind.Return && rentalId is null)
            throw new DomainException("inspection.rental_required", "A return inspection belongs to a rental that is out.");

        return new InspectionReport
        {
            Id = InspectionId.New(),
            ReservationId = reservationId,
            RentalId = rentalId,
            VehicleId = vehicleId,
            Kind = kind,
            Status = InspectionStatus.Draft,
            InspectedByUserId = inspectorUserId,
            InspectedByName = inspectorName,
            StartedAt = now,
            _checklist = Enum.GetValues<ChecklistItemCode>().Select(c => new ChecklistItem(c, ChecklistItemStatus.Unanswered, null)).ToList(),
        };
    }

    /// <summary>Save the agent's answers so far (draft). Unknown or duplicate checklist codes are refused.</summary>
    public void Update(int? odometerKm, FuelLevel? fuel, IReadOnlyCollection<ChecklistItem> items, IReadOnlyCollection<DamageMark> damage)
    {
        EnsureDraft();
        if (odometerKm is < 0)
            throw new DomainException("inspection.invalid_odometer", "Odometer reading cannot be negative.");
        if (items.GroupBy(i => i.Code).Any(g => g.Count() > 1) || items.Any(i => !Enum.IsDefined(i.Code)))
            throw new DomainException("inspection.invalid_checklist", "Each checklist item may appear once.");
        if (items.Any(i => i.Note is { Length: > MaxNoteLength }))
            throw new DomainException("inspection.note_too_long", $"Notes are at most {MaxNoteLength} characters.");

        OdometerKm = odometerKm;
        Fuel = fuel;
        var answers = items.ToDictionary(i => i.Code);
        _checklist = _checklist
            .Select(existing => answers.TryGetValue(existing.Code, out var answer)
                ? answer with { Note = string.IsNullOrWhiteSpace(answer.Note) ? null : answer.Note.Trim() }
                : existing)
            .ToList();
        _damage = damage.ToList();
    }

    public void AddAttachment(InspectionAttachment attachment)
    {
        EnsureCanAddAttachment();
        _attachments.Add(attachment);
    }

    /// <summary>Lets the upload use case refuse BEFORE storing a file (locked report, or already 20 files).</summary>
    public void EnsureCanAddAttachment()
    {
        EnsureDraft();
        if (_attachments.Count >= MaxAttachments)
            throw new DomainException("inspection.too_many_attachments", $"A report can have at most {MaxAttachments} photos and files.");
    }

    /// <summary>The agent picked a different car before completing the checkout report.</summary>
    public void ChangeVehicle(VehicleId vehicleId)
    {
        EnsureDraft();
        VehicleId = vehicleId;
    }

    /// <returns>The removed attachment (so the use case can delete the stored file).</returns>
    public InspectionAttachment RemoveAttachment(AttachmentId attachmentId)
    {
        EnsureDraft();
        var attachment = _attachments.SingleOrDefault(a => a.AttachmentId == attachmentId)
            ?? throw new DomainException("inspection.attachment_not_found", "That file is not on this report.");
        _attachments.Remove(attachment);
        return attachment;
    }

    /// <param name="checkoutReport">For a Return report: the completed Checkout report, to find NEW damage.</param>
    public void Complete(DateTimeOffset now, InspectionReport? checkoutReport = null)
    {
        EnsureDraft();

        var unanswered = _checklist.FirstOrDefault(i => i.Status == ChecklistItemStatus.Unanswered);
        if (unanswered is not null)
            throw new DomainException("inspection.incomplete", $"Answer every checklist item first ({unanswered.Code} is not answered).");
        var missingNote = _checklist.FirstOrDefault(i => i.NeedsNote && string.IsNullOrWhiteSpace(i.Note));
        if (missingNote is not null)
            throw new DomainException("inspection.note_required", $"Describe what is {missingNote.Status.ToString().ToLowerInvariant()} ({missingNote.Code}).");
        if (OdometerKm is null || Fuel is null)
            throw new DomainException("inspection.incomplete", "Record the odometer reading and the fuel level first.");

        if (Kind == InspectionKind.Checkout)
        {
            var missingAngles = RequiredCheckoutAngles.Where(angle => _attachments.All(a => a.Angle != angle || !a.ContentType.StartsWith("image/", StringComparison.Ordinal))).ToList();
            if (missingAngles.Count > 0)
                throw new DomainException("inspection.photos_required", $"A checkout report needs photos of the front, back, left and right (missing: {string.Join(", ", missingAngles)}).");
            if (_checklist.Single(i => i.Code == ChecklistItemCode.DriverIdAndLicenceVerified).Status != ChecklistItemStatus.Ok)
                throw new DomainException("inspection.driver_not_verified", "Verify the driver's ID and licence before handing over the keys.");
        }
        else
        {
            if (checkoutReport is null || checkoutReport.Kind != InspectionKind.Checkout || checkoutReport.Status != InspectionStatus.Completed
                || checkoutReport.ReservationId != ReservationId)
                throw new DomainException("inspection.checkout_report_required", "The completed checkout report is needed to compare damage.");
            _newDamage = FindNewDamage(checkoutReport).ToList();
        }

        Status = InspectionStatus.Completed;
        InspectedAt = now;
        Raise(new InspectionCompleted(Id, ReservationId, VehicleId, Kind, now));
        if (_newDamage.Count > 0)
            Raise(new NewDamageRecorded(Id, ReservationId, VehicleId, _newDamage.ToList(), now));
    }

    /// <summary>Links a completed Checkout report to the rental created by the handover.</summary>
    public void LinkRental(RentalId rentalId)
    {
        if (RentalId is not null && RentalId != rentalId)
            throw new DomainException("inspection.rental_mismatch", "This report belongs to another rental.");
        RentalId = rentalId;
    }

    /// <summary>The customer confirms they have seen the completed report (ownership checked by the use case).</summary>
    public void Acknowledge(DateTimeOffset now)
    {
        if (Status != InspectionStatus.Completed)
            throw new DomainException("inspection.not_completed", "Only a completed report can be acknowledged.");
        CustomerAcknowledgedAt ??= now;
    }

    private IEnumerable<NewDamage> FindNewDamage(InspectionReport checkout)
    {
        var damagedAtCheckout = checkout.Checklist.Where(i => i.HasDamage).Select(i => i.Code).ToHashSet();
        foreach (var item in _checklist.Where(i => i.HasDamage && !damagedAtCheckout.Contains(i.Code)))
            yield return new NewDamage(item.Code, item.Code.ToString(), item.Note ?? item.Status.ToString(), null);
        foreach (var mark in _damage.Where(m => !checkout.Damage.Any(m.SamePanel)))
            yield return new NewDamage(null, mark.Panel, mark.Description, mark.Severity);
    }

    private void EnsureDraft()
    {
        if (Status != InspectionStatus.Draft)
            throw new DomainException("inspection.locked", "A completed inspection is evidence and cannot be changed.");
    }
}

public sealed record InspectionCompleted(InspectionId InspectionId, ReservationId ReservationId, VehicleId VehicleId, InspectionKind Kind, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Damage found at return that was not there at checkout (damage charging will subscribe later).</summary>
public sealed record NewDamageRecorded(InspectionId InspectionId, ReservationId ReservationId, VehicleId VehicleId, IReadOnlyList<NewDamage> Damage, DateTimeOffset OccurredAt) : IDomainEvent;
