using CarRental.Application.Abstractions.Files;
using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Common.Customers;
using CarRental.Application.Common.Exceptions;
using CarRental.Application.Common.Files;
using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Inspections;

// ---------------------------------------------------------------------------------------------
// INSPECTION COMMANDS. Staff (Agent role, enforced by the API) start, fill, photograph and complete
// a report; the customer who owns the booking may acknowledge a completed one. Each command returns
// the report id; the API reads the fresh report back through the query side.
// ---------------------------------------------------------------------------------------------

/// <summary>
/// Start (or resume) the report of <paramref name="Kind"/> for a booking. Checkout: for the car the
/// agent chose (it can be changed while the report is a draft). Return: for the car on the rental.
/// </summary>
public sealed record StartInspectionCommand(Guid ReservationId, InspectionKind Kind, Guid? VehicleId) : ICommand<Guid>, IRetryOnConcurrencyConflict;

public sealed class StartInspectionValidator : AbstractValidator<StartInspectionCommand>
{
    public StartInspectionValidator()
    {
        RuleFor(c => c.ReservationId).NotEmpty();
        RuleFor(c => c.Kind).IsInEnum();
        RuleFor(c => c.VehicleId).NotEmpty().When(c => c.Kind == InspectionKind.Checkout)
            .WithMessage("Choose the car before starting the checkout inspection.");
    }
}

public sealed class StartInspectionHandler(
    ICurrentUser user,
    IReservationRepository reservations,
    IRentalRepository rentals,
    IVehicleRepository vehicles,
    IInspectionRepository inspections,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IRequestHandler<StartInspectionCommand, Guid>
{
    public async Task<Guid> Handle(StartInspectionCommand command, CancellationToken cancellationToken)
    {
        var agentId = user.UserId ?? throw new UnauthenticatedException();
        var reservationId = new ReservationId(command.ReservationId);
        _ = await reservations.GetAsync(reservationId, cancellationToken) ?? throw new NotFoundException("Reservation", command.ReservationId);

        RentalId? rentalId = null;
        VehicleId vehicleId;
        if (command.Kind == InspectionKind.Return)
        {
            var rental = await rentals.GetByReservationAsync(reservationId, cancellationToken)
                ?? throw new DomainException("inspection.rental_required", "This booking has not been checked out.");
            rentalId = rental.Id;
            vehicleId = rental.VehicleId;
        }
        else
        {
            var vehicle = await vehicles.GetAsync(new VehicleId(command.VehicleId!.Value), cancellationToken)
                ?? throw new NotFoundException("Vehicle", command.VehicleId!.Value);
            vehicleId = vehicle.Id;
        }

        // One report per kind per booking: resume it rather than start a second one.
        var existing = await inspections.GetAsync(reservationId, command.Kind, cancellationToken);
        if (existing is not null)
        {
            if (existing.VehicleId != vehicleId)
                existing.ChangeVehicle(vehicleId);   // draft only: a completed report is evidence
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return existing.Id.Value;
        }

        var report = InspectionReport.Start(reservationId, vehicleId, rentalId, command.Kind, agentId, InspectorName.From(user.Email), clock.GetUtcNow());
        inspections.Add(report);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return report.Id.Value;
    }
}

public sealed record ChecklistAnswer(ChecklistItemCode Code, ChecklistItemStatus Status, string? Note);

public sealed record DamageMarkInput(string Panel, string Description, DamageSeverity Severity);

/// <summary>Save the draft: odometer, fuel (eighths), checklist answers, damage marks.</summary>
public sealed record UpdateInspectionCommand(
    Guid InspectionId,
    int? OdometerKm,
    int? FuelEighths,
    IReadOnlyList<ChecklistAnswer> Checklist,
    IReadOnlyList<DamageMarkInput> Damage) : ICommand<Guid>;

public sealed class UpdateInspectionValidator : AbstractValidator<UpdateInspectionCommand>
{
    public UpdateInspectionValidator()
    {
        RuleFor(c => c.InspectionId).NotEmpty();
        RuleFor(c => c.Checklist).NotNull();
        RuleFor(c => c.Damage).NotNull().Must(d => d.Count <= 30).WithMessage("At most 30 damage marks.");
    }
}

public sealed class UpdateInspectionHandler(IInspectionRepository inspections, IUnitOfWork unitOfWork) : IRequestHandler<UpdateInspectionCommand, Guid>
{
    public async Task<Guid> Handle(UpdateInspectionCommand command, CancellationToken cancellationToken)
    {
        var report = await InspectionLookup.RequireAsync(inspections, command.InspectionId, cancellationToken);
        report.Update(
            command.OdometerKm,
            command.FuelEighths is { } eighths ? new FuelLevel(eighths) : null,
            command.Checklist.Select(i => new ChecklistItem(i.Code, i.Status, i.Note)).ToList(),
            command.Damage.Select(d => DamageMark.Create(d.Panel, d.Description, d.Severity)).ToList());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return report.Id.Value;
    }
}

/// <summary>
/// Upload one photo or document. The file is checked (type by magic bytes, 10 MB, metadata stripped)
/// before it is stored under a random key; only then is the metadata added to the report.
/// </summary>
public sealed record UploadInspectionAttachmentCommand(Guid InspectionId, string FileName, Stream Content, AttachmentAngle Angle) : ICommand<Guid>;

public sealed class UploadInspectionAttachmentHandler(
    ICurrentUser user,
    IInspectionRepository inspections,
    IFileStorage files,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IRequestHandler<UploadInspectionAttachmentCommand, Guid>
{
    public async Task<Guid> Handle(UploadInspectionAttachmentCommand command, CancellationToken cancellationToken)
    {
        var agentId = user.UserId ?? throw new UnauthenticatedException();
        var report = await InspectionLookup.RequireAsync(inspections, command.InspectionId, cancellationToken);
        report.EnsureCanAddAttachment();   // locked / full: refuse BEFORE storing anything

        var file = FileUploadPolicy.Check(await FileUploadPolicy.ReadLimitedAsync(command.Content, cancellationToken), command.FileName);
        using var content = new MemoryStream(file.Content, writable: false);
        var key = await files.SaveAsync(content, file.ContentType, cancellationToken);

        var attachment = new InspectionAttachment(AttachmentId.New(), file.FileName, file.ContentType, file.Content.Length,
            key, command.Angle, clock.GetUtcNow(), agentId);
        try
        {
            report.AddAttachment(attachment);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Compensation, not hiding: the metadata was not saved, so don't keep an orphan file.
            await files.DeleteAsync(key, CancellationToken.None);
            throw;
        }
        return attachment.AttachmentId.Value;
    }
}

public sealed record RemoveInspectionAttachmentCommand(Guid InspectionId, Guid AttachmentId) : ICommand<Unit>;

public sealed class RemoveInspectionAttachmentHandler(IInspectionRepository inspections, IFileStorage files, IUnitOfWork unitOfWork)
    : IRequestHandler<RemoveInspectionAttachmentCommand, Unit>
{
    public async Task<Unit> Handle(RemoveInspectionAttachmentCommand command, CancellationToken cancellationToken)
    {
        var report = await InspectionLookup.RequireAsync(inspections, command.InspectionId, cancellationToken);
        var removed = report.RemoveAttachment(new AttachmentId(command.AttachmentId));   // draft only
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await files.DeleteAsync(removed.StorageKey, cancellationToken);   // after the commit: never lose a file still referenced
        return Unit.Value;
    }
}

/// <summary>Complete the report (it becomes immutable evidence). A Return report is compared with the Checkout report.</summary>
public sealed record CompleteInspectionCommand(Guid InspectionId) : ICommand<Guid>;

public sealed class CompleteInspectionHandler(IInspectionRepository inspections, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<CompleteInspectionCommand, Guid>
{
    public async Task<Guid> Handle(CompleteInspectionCommand command, CancellationToken cancellationToken)
    {
        var report = await InspectionLookup.RequireAsync(inspections, command.InspectionId, cancellationToken);
        var checkout = report.Kind == InspectionKind.Return
            ? await inspections.GetAsync(report.ReservationId, InspectionKind.Checkout, cancellationToken)
            : null;

        report.Complete(clock.GetUtcNow(), checkout);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return report.Id.Value;
    }
}

/// <summary>The booking's owner confirms they have seen a completed report. Anyone else: 404.</summary>
public sealed record AcknowledgeInspectionCommand(Guid ReservationId, Guid InspectionId) : ICommand<Unit>;

public sealed class AcknowledgeInspectionHandler(
    CurrentCustomer currentCustomer,
    IReservationRepository reservations,
    IInspectionRepository inspections,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IRequestHandler<AcknowledgeInspectionCommand, Unit>
{
    public async Task<Unit> Handle(AcknowledgeInspectionCommand command, CancellationToken cancellationToken)
    {
        var customer = await currentCustomer.RequireAsync(cancellationToken);
        var reservation = await reservations.GetAsync(new ReservationId(command.ReservationId), cancellationToken);
        var report = await inspections.GetAsync(new InspectionId(command.InspectionId), cancellationToken);

        // Not yours, not this booking's, or not completed yet: "not found" (never confirm it exists).
        if (reservation is null || reservation.CustomerId != customer.Id || report is null
            || report.ReservationId != reservation.Id || report.Status != InspectionStatus.Completed)
            throw new NotFoundException("Inspection", command.InspectionId);

        report.Acknowledge(clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

internal static class InspectionLookup
{
    public static async Task<InspectionReport> RequireAsync(IInspectionRepository inspections, Guid id, CancellationToken cancellationToken) =>
        await inspections.GetAsync(new InspectionId(id), cancellationToken) ?? throw new NotFoundException("Inspection", id);
}

/// <summary>Customers see the inspector's FIRST name only. Identity has emails, so: "thandi.mokoena@roam.test" → "Thandi".</summary>
internal static class InspectorName
{
    public static string From(string? email)
    {
        var local = (email ?? "staff").Split('@')[0].Split('.', '_', '-')[0];
        return local.Length == 0 ? "Staff" : char.ToUpperInvariant(local[0]) + local[1..].ToLowerInvariant();
    }
}
