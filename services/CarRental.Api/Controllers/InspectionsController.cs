using CarRental.Application.Inspections;
using CarRental.Domain.Inspections;
using CarRental.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace CarRental.Api.Controllers;

/// <summary>Staff side of inspection reports (Agent role): start, fill, upload, complete.</summary>
[ApiController]
[Authorize(Roles = Roles.Agent)]
[Route("api/agent/inspections")]
[Produces("application/json")]
public sealed class AgentInspectionsController(ISender sender) : ControllerBase
{
    public sealed record StartRequest(Guid ReservationId, InspectionKind Kind, Guid? VehicleId);
    public sealed record UpdateRequest(int? OdometerKm, int? FuelEighths, IReadOnlyList<ChecklistAnswer> Checklist, IReadOnlyList<DamageMarkInput> Damage);

    /// <summary>Start (or resume) the Checkout/Return report for a booking. Returns the report.</summary>
    [HttpPost]
    [ProducesResponseType<InspectionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(StartRequest request, CancellationToken cancellationToken) =>
        await ReadBack(await sender.Send(new StartInspectionCommand(request.ReservationId, request.Kind, request.VehicleId), cancellationToken), cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<InspectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => await ReadBack(id, cancellationToken);

    [HttpPut("{id:guid}")]
    [ProducesResponseType<InspectionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, UpdateRequest request, CancellationToken cancellationToken) =>
        await ReadBack(await sender.Send(new UpdateInspectionCommand(id, request.OdometerKm, request.FuelEighths, request.Checklist ?? [], request.Damage ?? []), cancellationToken), cancellationToken);

    /// <summary>
    /// Upload one photo (JPEG/PNG) or PDF, max 10 MB, as multipart/form-data: "file" + "angle".
    /// The type is decided by the file's bytes; metadata (EXIF/GPS) is stripped; a random key is used.
    /// </summary>
    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<InspectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upload(Guid id, IFormFile file, [FromForm] AttachmentAngle angle, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        await sender.Send(new UploadInspectionAttachmentCommand(id, file.FileName, content, angle), cancellationToken);
        return await ReadBack(id, cancellationToken);
    }

    /// <summary>Remove a photo/file while the report is a draft.</summary>
    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType<InspectionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveInspectionAttachmentCommand(id, attachmentId), cancellationToken);
        return await ReadBack(id, cancellationToken);
    }

    /// <summary>Complete: 422 inspection.incomplete / note_required / photos_required / driver_not_verified.</summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType<InspectionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken) =>
        await ReadBack(await sender.Send(new CompleteInspectionCommand(id), cancellationToken), cancellationToken);

    private async Task<IActionResult> ReadBack(Guid id, CancellationToken cancellationToken) =>
        await sender.Send(new GetInspectionQuery(id), cancellationToken) is { } report ? Ok(report) : NotFound();
}

/// <summary>
/// Inspection files for anyone signed in: staff always; the booking's owner only once the report is
/// Completed; everyone else 404. Never served from a static path.
/// </summary>
[ApiController]
[Authorize]
[Route("api/inspections")]
public sealed class InspectionFilesController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var file = await sender.Send(new GetInspectionAttachmentQuery(id, attachmentId), cancellationToken);

        // nosniff: the browser must use our Content-Type (decided from the file's bytes), never guess.
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";

        var disposition = new ContentDispositionHeaderValue(file.ContentType.StartsWith("image/", StringComparison.Ordinal) ? "inline" : "attachment");
        disposition.SetHttpFileName(file.FileName);
        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
        return File(file.Content, file.ContentType);
    }
}
