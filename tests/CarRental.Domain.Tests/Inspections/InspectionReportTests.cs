using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using static CarRental.Domain.Tests.TestData;

namespace CarRental.Domain.Tests.Inspections;

public class InspectionReportTests
{
    private static readonly ReservationId Booking = ReservationId.New();
    private static readonly VehicleId Car = VehicleId.New();

    private static InspectionReport Checkout() => InspectionReport.Start(Booking, Car, null, InspectionKind.Checkout, "agent-1", "Thandi", Now);

    private static List<ChecklistItem> AllOk() =>
        Enum.GetValues<ChecklistItemCode>().Select(c => new ChecklistItem(c, ChecklistItemStatus.Ok, null)).ToList();

    private static List<ChecklistItem> With(ChecklistItemCode code, ChecklistItemStatus status, string? note = null) =>
        AllOk().Select(i => i.Code == code ? new ChecklistItem(code, status, note) : i).ToList();

    private static void AddPhotos(InspectionReport report, params AttachmentAngle[] angles)
    {
        foreach (var angle in angles)
            report.AddAttachment(new InspectionAttachment(AttachmentId.New(), $"{angle}.jpg", "image/jpeg", 1000, $"key-{Guid.NewGuid():N}", angle, Now, "agent-1"));
    }

    private static InspectionReport ReadyCheckout()
    {
        var report = Checkout();
        report.Update(10_000, FuelLevel.Full, AllOk(), []);
        AddPhotos(report, InspectionReport.RequiredCheckoutAngles);
        return report;
    }

    private static InspectionReport CompletedCheckout(List<ChecklistItem>? items = null, List<DamageMark>? damage = null)
    {
        var report = ReadyCheckout();
        report.Update(10_000, FuelLevel.Full, items ?? AllOk(), damage ?? []);
        report.Complete(Now);
        return report;
    }

    private static InspectionReport ReturnDraft() => InspectionReport.Start(Booking, Car, RentalId.New(), InspectionKind.Return, "agent-2", "Pieter", Now.AddDays(3));

    [Fact]
    public void A_new_report_is_a_draft_with_every_checklist_item_unanswered()
    {
        var report = Checkout();

        Assert.Equal(InspectionStatus.Draft, report.Status);
        Assert.Equal(12, report.Checklist.Count);
        Assert.All(report.Checklist, i => Assert.Equal(ChecklistItemStatus.Unanswered, i.Status));
    }

    [Fact]
    public void Cannot_complete_until_every_item_is_answered()
    {
        var report = Checkout();
        report.Update(10_000, FuelLevel.Full, AllOk().Take(11).ToList(), []);
        AddPhotos(report, InspectionReport.RequiredCheckoutAngles);

        Assert.Equal("inspection.incomplete", Assert.Throws<DomainException>(() => report.Complete(Now)).Code);
    }

    [Theory]
    [InlineData(ChecklistItemStatus.Damaged)]
    [InlineData(ChecklistItemStatus.Missing)]
    public void Damaged_or_missing_items_need_a_note(ChecklistItemStatus status)
    {
        var report = ReadyCheckout();
        report.Update(10_000, FuelLevel.Full, With(ChecklistItemCode.SpareWheelAndJack, status), []);

        Assert.Equal("inspection.note_required", Assert.Throws<DomainException>(() => report.Complete(Now)).Code);

        report.Update(10_000, FuelLevel.Full, With(ChecklistItemCode.SpareWheelAndJack, status, "Jack missing from boot"), []);
        report.Complete(Now);
        Assert.Equal(InspectionStatus.Completed, report.Status);
    }

    [Fact]
    public void Odometer_and_fuel_are_needed_to_complete()
    {
        var report = Checkout();
        report.Update(null, null, AllOk(), []);
        AddPhotos(report, InspectionReport.RequiredCheckoutAngles);

        Assert.Equal("inspection.incomplete", Assert.Throws<DomainException>(() => report.Complete(Now)).Code);
    }

    [Fact]
    public void A_checkout_report_needs_photos_of_the_front_back_left_and_right()
    {
        var report = Checkout();
        report.Update(10_000, FuelLevel.Full, AllOk(), []);
        AddPhotos(report, AttachmentAngle.Front, AttachmentAngle.Back, AttachmentAngle.Left, AttachmentAngle.Interior);

        var ex = Assert.Throws<DomainException>(() => report.Complete(Now));
        Assert.Equal("inspection.photos_required", ex.Code);
        Assert.Contains("Right", ex.Message);
    }

    [Fact]
    public void A_checkout_report_needs_the_driver_verified()
    {
        var report = ReadyCheckout();
        report.Update(10_000, FuelLevel.Full, With(ChecklistItemCode.DriverIdAndLicenceVerified, ChecklistItemStatus.NotApplicable), []);

        Assert.Equal("inspection.driver_not_verified", Assert.Throws<DomainException>(() => report.Complete(Now)).Code);
    }

    [Fact]
    public void Fuel_is_measured_in_eighths()
    {
        Assert.Equal("inspection.invalid_fuel", Assert.Throws<DomainException>(() => new FuelLevel(9)).Code);
        Assert.Equal("inspection.invalid_fuel", Assert.Throws<DomainException>(() => new FuelLevel(-1)).Code);
    }

    [Fact]
    public void A_report_holds_at_most_20_attachments()
    {
        var report = Checkout();
        AddPhotos(report, Enumerable.Repeat(AttachmentAngle.Other, InspectionReport.MaxAttachments).ToArray());

        Assert.Equal("inspection.too_many_attachments", Assert.Throws<DomainException>(() => AddPhotos(report, AttachmentAngle.Front)).Code);
    }

    [Fact]
    public void A_completed_report_is_evidence_and_cannot_change()
    {
        var report = CompletedCheckout();

        Assert.Equal("inspection.locked", Assert.Throws<DomainException>(() => report.Update(1, FuelLevel.Full, AllOk(), [])).Code);
        Assert.Equal("inspection.locked", Assert.Throws<DomainException>(() => AddPhotos(report, AttachmentAngle.Other)).Code);
        Assert.Equal("inspection.locked", Assert.Throws<DomainException>(() => report.RemoveAttachment(report.Attachments[0].AttachmentId)).Code);
        Assert.Equal("inspection.locked", Assert.Throws<DomainException>(() => report.Complete(Now)).Code);
    }

    [Fact]
    public void Attachments_can_be_removed_while_draft()
    {
        var report = ReadyCheckout();
        var first = report.Attachments[0];

        Assert.Equal(first, report.RemoveAttachment(first.AttachmentId));
        Assert.Equal(3, report.Attachments.Count);
    }

    [Fact]
    public void A_return_report_lists_only_damage_that_was_not_there_at_checkout()
    {
        var checkout = CompletedCheckout(
            With(ChecklistItemCode.ExteriorRear, ChecklistItemStatus.Damaged, "Scuff on bumper"),
            [DamageMark.Create("Rear bumper", "Scuff 5 cm", DamageSeverity.Minor)]);
        var report = ReturnDraft();
        var items = AllOk().Select(i => i.Code switch
        {
            ChecklistItemCode.ExteriorRear => new ChecklistItem(i.Code, ChecklistItemStatus.Damaged, "Same scuff"),
            ChecklistItemCode.Windscreen => new ChecklistItem(i.Code, ChecklistItemStatus.Damaged, "Chip, passenger side"),
            _ => i,
        }).ToList();
        report.Update(11_200, new FuelLevel(6), items,
            [DamageMark.Create("rear BUMPER", "Scuff 5 cm", DamageSeverity.Minor), DamageMark.Create("Left door", "Dent 3 cm", DamageSeverity.Moderate)]);
        report.ClearDomainEvents();

        report.Complete(Now.AddDays(3), checkout);

        Assert.Equal(new[] { "Windscreen", "Left door" }, report.NewDamage.Select(d => d.Panel));
        var recorded = Assert.Single(report.DomainEvents.OfType<NewDamageRecorded>());
        Assert.Equal(2, recorded.Damage.Count);
    }

    [Fact]
    public void A_return_report_needs_the_completed_checkout_report()
    {
        var report = ReturnDraft();
        report.Update(11_000, FuelLevel.Full, AllOk(), []);

        Assert.Equal("inspection.checkout_report_required", Assert.Throws<DomainException>(() => report.Complete(Now)).Code);
        Assert.Equal("inspection.checkout_report_required", Assert.Throws<DomainException>(() => report.Complete(Now, Checkout())).Code);
    }

    [Fact]
    public void A_return_report_does_not_need_photos_or_driver_verification()
    {
        var report = ReturnDraft();
        report.Update(11_000, FuelLevel.Full, With(ChecklistItemCode.DriverIdAndLicenceVerified, ChecklistItemStatus.NotApplicable), []);

        report.Complete(Now, CompletedCheckout());

        Assert.Empty(report.NewDamage);
        Assert.Empty(report.DomainEvents.OfType<NewDamageRecorded>());
    }

    [Fact]
    public void A_return_inspection_belongs_to_a_rental() =>
        Assert.Equal("inspection.rental_required", Assert.Throws<DomainException>(() =>
            InspectionReport.Start(Booking, Car, null, InspectionKind.Return, "agent-1", "Thandi", Now)).Code);

    [Fact]
    public void Only_a_completed_report_can_be_acknowledged_and_the_first_time_is_kept()
    {
        Assert.Equal("inspection.not_completed", Assert.Throws<DomainException>(() => Checkout().Acknowledge(Now)).Code);

        var report = CompletedCheckout();
        report.Acknowledge(Now.AddHours(1));
        report.Acknowledge(Now.AddHours(2));

        Assert.Equal(Now.AddHours(1), report.CustomerAcknowledgedAt);
    }
}
