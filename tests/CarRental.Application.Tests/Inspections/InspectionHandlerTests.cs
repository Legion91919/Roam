using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Common.Exceptions;
using CarRental.Application.Inspections;
using CarRental.Application.Tests.Fakes;
using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using static CarRental.Application.Tests.Fakes.TestDomain;

namespace CarRental.Application.Tests.Inspections;

/// <summary>Uploads (checked before anything is stored), downloads (owner only after completion) and acknowledgement.</summary>
public class InspectionHandlerTests
{
    private readonly InMemoryInspectionRepository _inspections = new();
    private readonly InMemoryFileStorage _files = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentUser _agent = new("agent-1", "thandi@roam.test");

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>A tiny but well-formed PNG: signature, IHDR, a tEXt chunk (metadata), IEND.</summary>
    private static byte[] Png(bool withTextChunk = false)
    {
        static byte[] Chunk(string type, byte[] data)
        {
            var bytes = new byte[12 + data.Length];
            bytes[0] = (byte)(data.Length >> 24); bytes[1] = (byte)(data.Length >> 16); bytes[2] = (byte)(data.Length >> 8); bytes[3] = (byte)data.Length;
            System.Text.Encoding.ASCII.GetBytes(type).CopyTo(bytes, 4);
            data.CopyTo(bytes, 8);
            return bytes;   // CRC left as zeros: the policy does not validate CRCs
        }
        var parts = new List<byte[]> { PngSignature, Chunk("IHDR", new byte[13]) };
        if (withTextChunk) parts.Add(Chunk("tEXt", System.Text.Encoding.ASCII.GetBytes("GPS\0-26.1,28.2")));
        parts.Add(Chunk("IEND", []));
        return parts.SelectMany(p => p).ToArray();
    }

    private InspectionReport DraftCheckout()
    {
        var report = InspectionReport.Start(ReservationId.New(), VehicleId.New(), null, InspectionKind.Checkout, "agent-1", "Thandi", Now);
        _inspections.Add(report);
        return report;
    }

    private UploadInspectionAttachmentHandler UploadHandler() => new(_agent, _inspections, _files, _unitOfWork, new FixedClock(Now));

    private Task<Guid> UploadAsync(InspectionReport report, byte[] bytes, string name = "front.png") =>
        UploadHandler().Handle(new UploadInspectionAttachmentCommand(report.Id.Value, name, new MemoryStream(bytes), AttachmentAngle.Front), CancellationToken.None);

    [Fact]
    public async Task A_png_photo_is_stored_under_a_random_key_with_its_metadata_stripped()
    {
        var report = DraftCheckout();

        await UploadAsync(report, Png(withTextChunk: true), "../../etc/My Car.png");

        var attachment = Assert.Single(report.Attachments);
        var stored = _files.Files[attachment.StorageKey];
        Assert.Equal("image/png", attachment.ContentType);
        Assert.Equal("My Car.png", attachment.FileName);               // no path from the user's file name
        Assert.DoesNotContain("My Car", attachment.StorageKey);        // the key never comes from the file name
        Assert.Equal(Png(withTextChunk: false), stored.Bytes);         // tEXt (e.g. GPS) chunk removed
        Assert.Equal(1, _unitOfWork.Commits);
    }

    [Fact]
    public async Task A_file_whose_bytes_are_not_jpeg_png_or_pdf_is_refused_even_if_named_like_a_photo()
    {
        var report = DraftCheckout();
        var windowsExecutable = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };   // "MZ" header

        var ex = await Assert.ThrowsAsync<DomainException>(() => UploadAsync(report, windowsExecutable, "front.jpg"));

        Assert.Equal("file.type_not_allowed", ex.Code);
        Assert.Empty(_files.Files);
        Assert.Empty(report.Attachments);
    }

    [Fact]
    public async Task A_file_over_10_MB_is_refused_and_nothing_is_stored()
    {
        var report = DraftCheckout();
        var big = new byte[10 * 1024 * 1024 + 1];
        PngSignature.CopyTo(big, 0);

        var ex = await Assert.ThrowsAsync<DomainException>(() => UploadAsync(report, big));

        Assert.Equal("file.too_large", ex.Code);
        Assert.Empty(_files.Files);
    }

    [Fact]
    public async Task A_completed_report_accepts_no_more_files_and_nothing_is_stored()
    {
        var report = CompletedCheckout(ReservationId.New(), VehicleId.New(), 42_000);
        _inspections.Add(report);

        var ex = await Assert.ThrowsAsync<DomainException>(() => UploadAsync(report, Png()));

        Assert.Equal("inspection.locked", ex.Code);
        Assert.Empty(_files.Files);
    }

    // ---- Downloads: staff always; the owner only once completed; everyone else "not found" ----

    private sealed class FakeInspectionReadModel(AttachmentAccess? access) : IInspectionReadModel
    {
        public Task<InspectionDto?> GetAsync(Guid inspectionId, CancellationToken cancellationToken) => Task.FromResult<InspectionDto?>(null);
        public Task<IReadOnlyList<InspectionDto>> ListCompletedForOwnerAsync(Guid reservationId, string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<InspectionDto>>([]);
        public Task<IReadOnlyList<InspectionSummaryDto>> ListForReservationAsync(Guid reservationId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<InspectionSummaryDto>>([]);
        public Task<AttachmentAccess?> GetAttachmentAsync(Guid inspectionId, Guid attachmentId, CancellationToken cancellationToken) => Task.FromResult(access);
    }

    private async Task<FileDownload> DownloadAsync(ICurrentUser user, bool completed)
    {
        var key = await _files.SaveAsync(new MemoryStream(Png()), "image/png", CancellationToken.None);
        var readModel = new FakeInspectionReadModel(new AttachmentAccess(key, "image/png", "front.png", completed, OwnerUserId: "user-1"));
        return await new GetInspectionAttachmentHandler(user, readModel, _files).Handle(new GetInspectionAttachmentQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
    }

    [Fact]
    public async Task The_owner_can_download_a_photo_once_the_report_is_completed()
    {
        var file = await DownloadAsync(new FakeCurrentUser("user-1"), completed: true);

        Assert.Equal("image/png", file.ContentType);
    }

    [Fact]
    public async Task The_owner_cannot_see_files_of_a_draft_report()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => DownloadAsync(new FakeCurrentUser("user-1"), completed: false));
    }

    [Fact]
    public async Task Another_customer_gets_not_found_for_someone_elses_file()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => DownloadAsync(new FakeCurrentUser("someone-else"), completed: true));
    }

    [Fact]
    public async Task Staff_can_download_files_of_draft_reports()
    {
        _agent.RoleList.Add(StaffRoles.Agent);

        var file = await DownloadAsync(_agent, completed: false);

        Assert.Equal("front.png", file.FileName);
    }

    // ---- Acknowledgement by the booking's owner ----

    [Fact]
    public async Task A_customer_cannot_acknowledge_a_draft_report_it_is_not_found()
    {
        var signedIn = new SignedInCustomer();
        var reservations = new InMemoryReservationRepository();
        var reservation = OwnedReservation(signedIn);
        reservations.Add(reservation);
        var draft = InspectionReport.Start(reservation.Id, VehicleId.New(), null, InspectionKind.Checkout, "agent-1", "Thandi", Now);
        _inspections.Add(draft);

        await Assert.ThrowsAsync<NotFoundException>(() => new AcknowledgeInspectionHandler(signedIn.Current, reservations, _inspections, _unitOfWork, new FixedClock(Now))
            .Handle(new AcknowledgeInspectionCommand(reservation.Id.Value, draft.Id.Value), CancellationToken.None));
    }

    [Fact]
    public async Task Another_customer_cannot_acknowledge_my_report()
    {
        var signedIn = new SignedInCustomer().AddOtherCustomer("someone-else");
        var reservations = new InMemoryReservationRepository();
        var reservation = OwnedReservation(signedIn);
        reservations.Add(reservation);
        var report = CompletedCheckout(reservation.Id, VehicleId.New(), 42_000);
        _inspections.Add(report);
        signedIn.SwitchTo("someone-else");

        await Assert.ThrowsAsync<NotFoundException>(() => new AcknowledgeInspectionHandler(signedIn.Current, reservations, _inspections, _unitOfWork, new FixedClock(Now))
            .Handle(new AcknowledgeInspectionCommand(reservation.Id.Value, report.Id.Value), CancellationToken.None));
        Assert.False(report.CustomerAcknowledged);
    }

    [Fact]
    public async Task The_owner_acknowledges_a_completed_report()
    {
        var signedIn = new SignedInCustomer();
        var reservations = new InMemoryReservationRepository();
        var reservation = OwnedReservation(signedIn);
        reservations.Add(reservation);
        var report = CompletedCheckout(reservation.Id, VehicleId.New(), 42_000);
        _inspections.Add(report);

        await new AcknowledgeInspectionHandler(signedIn.Current, reservations, _inspections, _unitOfWork, new FixedClock(Now))
            .Handle(new AcknowledgeInspectionCommand(reservation.Id.Value, report.Id.Value), CancellationToken.None);

        Assert.True(report.CustomerAcknowledged);
    }

    private static CarRental.Domain.Reservations.Reservation OwnedReservation(SignedInCustomer signedIn)
    {
        var request = new CarRental.Domain.Pricing.PricingRequest(CarRental.Domain.ValueObjects.VehicleCategory.Economy,
            CarRental.Domain.ValueObjects.RentalPeriod.Create(Pickup, Pickup.AddDays(2)), signedIn.Customer.ToDriverProfile(), Jnb, Jnb,
            CarRental.Domain.ValueObjects.Extras.None);
        return CarRental.Domain.Reservations.Reservation.Create(signedIn.Customer.Id, request.Category, Jnb, Jnb, request.Period,
            request.Driver, request.Extras, Pricer().Quote(request),
            CarRental.Domain.Terms.TermsAcceptance.Accept(FakeTermsProvider.Version, FakeTermsProvider.Version, Now), Now);
    }
}
