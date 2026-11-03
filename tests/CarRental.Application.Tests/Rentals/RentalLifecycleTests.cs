using CarRental.Application.Reservations.CreateReservation;
using CarRental.Application.Rentals.CheckOutVehicle;
using CarRental.Application.Rentals.ReturnVehicle;
using CarRental.Application.Tests.Fakes;
using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using CarRental.Domain.Payments;
using CarRental.Domain.Pricing;
using CarRental.Domain.Rentals;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;
using static CarRental.Application.Tests.Fakes.TestDomain;

namespace CarRental.Application.Tests.Rentals;

/// <summary>
/// Book -> inspect -> check out a real fleet car -> inspect -> return, across the handlers, with a
/// clock we move by hand. Keys only change hands after a completed inspection.
/// </summary>
public class RentalLifecycleTests
{
    private readonly InMemoryInventoryRepository _inventory = new InMemoryInventoryRepository()
        .Seed(Jnb, VehicleCategory.Economy, PickupDay, 20, 3)
        .Seed(Cpt, VehicleCategory.Economy, PickupDay, 20, 3);
    private readonly InMemoryReservationRepository _reservations = new();
    private readonly InMemoryRentalRepository _rentals = new();
    private readonly InMemoryVehicleRepository _vehicles = new();
    private readonly InMemoryInspectionRepository _inspections = new();
    private readonly InMemoryPaymentRepository _payments = new();
    private readonly FakePaymentGateway _gateway = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new(Now);

    private CheckOutVehicleHandler CheckOutHandler() => new(_reservations, _vehicles, _inspections, _payments, _rentals, _unitOfWork, RateCard.Standard, _clock);
    private ReturnVehicleHandler ReturnHandler() => new(_rentals, _reservations, _vehicles, _inspections, _payments, _gateway, _unitOfWork, _clock);

    private async Task<Guid> BookAsync(int days, BranchId? pickup = null, BranchId? returnTo = null)
    {
        var from = pickup ?? Jnb;
        var booking = await new CreateReservationHandler(new SignedInCustomer(ageOnPickup: 30).Current, new InMemoryBranchRepository(Jnb, Cpt),
                _inventory, _reservations, _payments, _gateway, _unitOfWork, new FakeTermsProvider(), Pricer(), _clock)
            .Handle(new CreateReservationCommand(VehicleCategory.Economy, from.Value, (returnTo ?? from).Value,
                Pickup, Pickup.AddDays(days), FakeTermsProvider.Version, PaymentToken: ApprovedCard), CancellationToken.None);
        return booking.ReservationId;
    }

    /// <summary>A car at the pickup branch with a completed checkout inspection, handed over at pickup time.</summary>
    private async Task<(Guid RentalId, Vehicle Vehicle)> CheckOutAsync(Guid reservationId, BranchId? branch = null)
    {
        var vehicle = NewVehicle(VehicleCategory.Economy, branch ?? Jnb, odometerKm: 42_000);
        _vehicles.Add(vehicle);
        _inspections.Add(CompletedCheckout(new ReservationId(reservationId), vehicle.Id, 42_000));

        _clock.Now = Pickup;   // customer arrives at the counter
        var rentalId = await CheckOutHandler().Handle(new CheckOutVehicleCommand(reservationId, vehicle.Id.Value), CancellationToken.None);
        return (rentalId, vehicle);
    }

    private async Task<(Guid RentalId, Vehicle Vehicle)> CheckedOutRentalAsync(int days = 3) => await CheckOutAsync(await BookAsync(days));

    /// <summary>The agent completes the return inspection at <paramref name="at"/> with the odometer reading.</summary>
    private void InspectReturn(Guid rentalId, int odometerKm, DateTimeOffset at)
    {
        var rental = _rentals.Items.Single(r => r.Id.Value == rentalId);
        var checkout = _inspections.Items.Single(i => i.ReservationId == rental.ReservationId && i.Kind == InspectionKind.Checkout);
        _inspections.Add(CompletedReturn(checkout, rental.Id, odometerKm, at));
        _clock.Now = at;
    }

    [Fact]
    public async Task Checkout_hands_over_the_inspected_car_and_puts_it_on_rent()
    {
        var (rentalId, vehicle) = await CheckedOutRentalAsync();

        var rental = _rentals.Items.Single();
        Assert.Equal(rentalId, rental.Id.Value);
        Assert.Equal(vehicle.Id, rental.VehicleId);
        Assert.Equal(42_000, rental.StartOdometerKm);   // from the inspection, not typed again
        Assert.Equal(VehicleStatus.OnRent, vehicle.Status);
        Assert.Equal(rental.Id, _inspections.Items.Single().RentalId);   // report linked to the rental
    }

    [Fact]
    public async Task Checkout_without_a_completed_inspection_is_refused_and_nothing_is_saved()
    {
        var reservationId = await BookAsync(days: 3);
        var vehicle = NewVehicle();
        _vehicles.Add(vehicle);
        var draft = InspectionReport.Start(new ReservationId(reservationId), vehicle.Id, null, InspectionKind.Checkout, "agent-1", "Thandi", Now);
        _inspections.Add(draft);   // started but never completed
        var commitsBefore = _unitOfWork.Commits;
        _clock.Now = Pickup;

        var ex = await Assert.ThrowsAsync<DomainException>(() => CheckOutHandler().Handle(new CheckOutVehicleCommand(reservationId, vehicle.Id.Value), CancellationToken.None));

        Assert.Equal("rental.inspection_required", ex.Code);
        Assert.Empty(_rentals.Items);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.Equal(commitsBefore, _unitOfWork.Commits);
    }

    [Fact]
    public async Task Return_checks_the_car_back_in_with_the_inspected_odometer()
    {
        var (rentalId, vehicle) = await CheckedOutRentalAsync(days: 3);
        InspectReturn(rentalId, 42_850, Pickup.AddDays(3));

        var result = await ReturnHandler().Handle(new ReturnVehicleCommand(rentalId), CancellationToken.None);

        Assert.Equal(850, result.DistanceKm);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.Equal(42_850, vehicle.OdometerKm);
        Assert.Equal(Jnb, vehicle.CurrentBranchId);
        Assert.Equal(RentalStatus.Closed, _rentals.Items.Single().Status);
    }

    [Fact]
    public async Task A_one_way_return_moves_the_car_to_the_return_branch()
    {
        var (rentalId, vehicle) = await CheckOutAsync(await BookAsync(days: 3, pickup: Jnb, returnTo: Cpt));
        InspectReturn(rentalId, 43_400, Pickup.AddDays(3));

        await ReturnHandler().Handle(new ReturnVehicleCommand(rentalId), CancellationToken.None);

        Assert.Equal(Cpt, vehicle.CurrentBranchId);
        Assert.Equal(Jnb, vehicle.HomeBranchId);
    }

    [Fact]
    public async Task Return_without_a_completed_return_inspection_is_refused()
    {
        var (rentalId, vehicle) = await CheckedOutRentalAsync();
        _clock.Now = Pickup.AddDays(3);

        var ex = await Assert.ThrowsAsync<DomainException>(() => ReturnHandler().Handle(new ReturnVehicleCommand(rentalId), CancellationToken.None));

        Assert.Equal("rental.inspection_required", ex.Code);
        Assert.Equal(VehicleStatus.OnRent, vehicle.Status);
        Assert.Empty(_gateway.Captures);
    }

    [Fact]
    public async Task Returning_an_hour_late_charges_one_day_late_fee()
    {
        // 14-day booking: R4 165 hold, R624,75 headroom keeps the R525 fee inside the 15% capture rule.
        var (rentalId, _) = await CheckedOutRentalAsync(days: 14);
        InspectReturn(rentalId, 42_850, Pickup.AddDays(14).AddHours(1));

        var result = await ReturnHandler().Handle(new ReturnVehicleCommand(rentalId), CancellationToken.None);

        Assert.Equal(350m * 1.5m, result.LateFee);
    }

    [Fact]
    public async Task An_on_time_return_captures_the_booking_total()
    {
        var (rentalId, _) = await CheckedOutRentalAsync(days: 3);
        InspectReturn(rentalId, 42_300, Pickup.AddDays(3));

        var result = await ReturnHandler().Handle(new ReturnVehicleCommand(rentalId), CancellationToken.None);

        Assert.Equal(Money.Zar(1050m), Assert.Single(_gateway.Captures));
        Assert.Equal("Captured", result.Payment!.Status);
        Assert.Equal(1050m, result.Payment.Captured);
    }

    [Fact]
    public async Task A_late_return_captures_the_booking_total_plus_the_late_fee()
    {
        // 12 days: 12 x R350 = R4 200 less 15% weekly = R3 570 hold. One started day late = R525:
        // R4 095 due, inside the 15% headroom (R4 105,50).
        var (rentalId, _) = await CheckedOutRentalAsync(days: 12);
        InspectReturn(rentalId, 43_000, Pickup.AddDays(12).AddHours(2));

        var result = await ReturnHandler().Handle(new ReturnVehicleCommand(rentalId), CancellationToken.None);

        Assert.Equal(525m, result.LateFee);
        Assert.Equal(4095m, result.AmountDue);
        Assert.Equal(Money.Zar(4095m), Assert.Single(_gateway.Captures));
        Assert.Equal(PaymentStatus.Captured, _payments.Items.Single().Status);
    }

    [Fact]
    public async Task A_late_fee_beyond_15_percent_of_the_hold_is_refused_before_the_gateway_is_asked()
    {
        var (rentalId, _) = await CheckedOutRentalAsync(days: 3);   // R1 050 hold, limit R1 207,50
        InspectReturn(rentalId, 42_300, Pickup.AddDays(3).AddHours(2));   // R525 late fee: R1 575 due
        var commitsBefore = _unitOfWork.Commits;

        var ex = await Assert.ThrowsAsync<DomainException>(() => ReturnHandler().Handle(new ReturnVehicleCommand(rentalId), CancellationToken.None));

        Assert.Equal("payment.capture_exceeds_authorization", ex.Code);
        Assert.Empty(_gateway.Captures);
        Assert.Equal(commitsBefore, _unitOfWork.Commits);
    }

    [Fact]
    public async Task A_declined_capture_still_records_the_return_and_marks_the_payment_failed()
    {
        var (rentalId, _) = await CheckedOutRentalAsync(days: 3);
        _gateway.DeclineCaptures = true;
        InspectReturn(rentalId, 42_300, Pickup.AddDays(3));

        var result = await ReturnHandler().Handle(new ReturnVehicleCommand(rentalId), CancellationToken.None);

        Assert.Equal(RentalStatus.Closed, _rentals.Items.Single().Status);
        Assert.Equal("Failed", result.Payment!.Status);
        Assert.Equal("card_declined", result.Payment.FailureReason);
    }

    [Fact]
    public async Task An_inspected_odometer_lower_than_at_checkout_is_rejected_and_not_saved()
    {
        var (rentalId, vehicle) = await CheckedOutRentalAsync();
        InspectReturn(rentalId, 41_999, Pickup.AddDays(3));
        var commitsBefore = _unitOfWork.Commits;

        var ex = await Assert.ThrowsAsync<DomainException>(() => ReturnHandler().Handle(new ReturnVehicleCommand(rentalId), CancellationToken.None));

        Assert.Equal("rental.odometer_decreased", ex.Code);
        Assert.Equal(commitsBefore, _unitOfWork.Commits);
        Assert.Equal(VehicleStatus.OnRent, vehicle.Status);
        Assert.Empty(_gateway.Captures);
    }

    [Fact]
    public async Task The_agents_branch_is_irrelevant_to_checkout_any_agent_can_hand_over_any_branchs_car()
    {
        // Agents have no home branch in the model yet: the branch picked in the staff app is a UI
        // filter only (trade-off documented in docs/ARCHITECTURE.md).
        var reservationId = await BookAsync(days: 2, pickup: Cpt);

        var (rentalId, _) = await CheckOutAsync(reservationId, Cpt);

        Assert.Equal(rentalId, _rentals.Items.Single().Id.Value);
    }
}
