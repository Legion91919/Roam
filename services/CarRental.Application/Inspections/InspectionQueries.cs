using CarRental.Application.Abstractions.Files;
using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Common.Exceptions;
using MediatR;

namespace CarRental.Application.Inspections;

/// <summary>Staff: one report, draft or completed.</summary>
public sealed record GetInspectionQuery(Guid InspectionId) : IQuery<InspectionDto?>;

public sealed class GetInspectionHandler(IInspectionReadModel readModel) : IRequestHandler<GetInspectionQuery, InspectionDto?>
{
    public Task<InspectionDto?> Handle(GetInspectionQuery query, CancellationToken cancellationToken) => readModel.GetAsync(query.InspectionId, cancellationToken);
}

/// <summary>Customer: the COMPLETED reports of their own booking (others' bookings: empty → 404 at the API).</summary>
public sealed record GetMyReservationInspectionsQuery(Guid ReservationId) : IQuery<IReadOnlyList<InspectionDto>>;

public sealed class GetMyReservationInspectionsHandler(ICurrentUser user, IInspectionReadModel readModel)
    : IRequestHandler<GetMyReservationInspectionsQuery, IReadOnlyList<InspectionDto>>
{
    public Task<IReadOnlyList<InspectionDto>> Handle(GetMyReservationInspectionsQuery query, CancellationToken cancellationToken) =>
        readModel.ListCompletedForOwnerAsync(query.ReservationId, user.UserId ?? throw new UnauthenticatedException(), cancellationToken);
}

public sealed record FileDownload(Stream Content, string ContentType, string FileName);

/// <summary>
/// Download one photo/file. Staff: always. The booking's owner: only once the report is Completed.
/// Everyone else, and drafts for customers: "not found" (never confirm a file exists).
/// </summary>
public sealed record GetInspectionAttachmentQuery(Guid InspectionId, Guid AttachmentId) : IQuery<FileDownload>;

public sealed class GetInspectionAttachmentHandler(ICurrentUser user, IInspectionReadModel readModel, IFileStorage files)
    : IRequestHandler<GetInspectionAttachmentQuery, FileDownload>
{
    public async Task<FileDownload> Handle(GetInspectionAttachmentQuery query, CancellationToken cancellationToken)
    {
        var userId = user.UserId ?? throw new UnauthenticatedException();
        var access = await readModel.GetAttachmentAsync(query.InspectionId, query.AttachmentId, cancellationToken);

        var allowed = access is not null && (user.IsInRole(StaffRoles.Agent) || (access.Completed && access.OwnerUserId == userId));
        if (!allowed)
            throw new NotFoundException("Attachment", query.AttachmentId);

        return new FileDownload(await files.OpenReadAsync(access!.StorageKey, cancellationToken), access.ContentType, access.FileName);
    }
}
