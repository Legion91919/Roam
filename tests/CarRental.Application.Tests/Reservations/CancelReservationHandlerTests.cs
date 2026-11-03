using CarRental.Application.Common.Exceptions;
using CarRental.Application.Reservations.CancelReservation;
using CarRental.Application.Reservations.Cancellation;
using CarRental.Application.Reservations.CreateReservation;
using CarRental.Application.Tests.Fakes;
using CarRental.Domain.Common;
using CarRental.Domain.Payments;
using CarRental.Domain.Pricing;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;
using static CarRental.Application.Tests.Fakes.TestDomain;

namespace CarRental.Application.Tests.Reservations;

/// <summary>Customer cancellation, agent cancellation (reason required) and no-show, with inventory and the card hold.</summary>
public class CancelReservationHandlerTests
{
    private readonly InMemoryInventoryRepository _inventory = new InMemoryInventoryRepository().Seed(Jnb, VehicleCategory.Suv, PickupDay, 10, capacity: 1);
    private readonly InMemoryReservationRepository _reservations = new();
    private readonly InMemoryPaymentRepository _payments = new();
    private readonly FakePaymentGateway _gateway = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new(Now);
    private readonly SignedInCustomer _signedIn = new SignedInCustomer().AddOtherCustomer("someone-else");
    private readonly FakeCurrentUser _agent = new("agent-1", "thandi@roam.test");

    private BookingRelease Release() => new(_inventory, _payments, _gateway, new OneDayRateNoShowFee(RateCard.Standard));

    private async Task<Guid> BookAsync()
    {
        var create = new CreateReservationHandler(_signedIn.Current, new InMemoryBranchRepository(Jnb), _inventory, _reservations, _payments, _gateway, _unitOfWork, new FakeTermsProvider(), Pricer(), _clock);
        var result = await create.Handle(new CreateReservationCommand(VehicleCategory.Suv, Jnb.Value, Jnb.Value,
            Pickup, Pickup.AddDays(2), FakeTermsProvider.Version, PaymentToken: ApprovedCard), CancellationToken.None);
        return result.ReservationId;
    }

    private CancelReservationHandler CustomerHandler() => new(_signedIn.Current, _reservations, Release(), _unitOfWork, _clock);
    private AgentCancelReservationHandler AgentHandler() => new(_agent, _reservations, Release(), _unitOfWork, _clock);
    private MarkNoShowHandler NoShowHandler() => new(_agent, _reservations, Release(), _unitOfWork, _clock);

    [Fact]
    public async Task Cancelling_gives_the_car_back_to_inventory()
    {
        var id = await BookAsync();

        await CustomerHandler().Handle(new CancelReservationCommand(id), CancellationToken.None);

        var reservation = _reservations.Items.Single();
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(CancelledBy.Customer, reservation.Cancellation!.By);
        Assert.All(_inventory.Items, i => Assert.Equal(0, i.Reserved));
        Assert.Equal(2, _unitOfWork.Commits);   // one for the booking, one for the cancellation
    }

    [Fact]
    public async Task Cancelling_voids_the_card_authorisation()
    {
        var id = await BookAsync();

        await CustomerHandler().Handle(new CancelReservationCommand(id), CancellationToken.None);

        Assert.Equal(1, _gateway.Voids);
        Assert.Equal(PaymentStatus.Voided, _payments.Items.Single().Status);
    }

    [Fact]
    public async Task Another_customer_cannot_cancel_it_and_cannot_even_see_it_exists()
    {
        var id = await BookAsync();
        _signedIn.SwitchTo("someone-else");

        await Assert.ThrowsAsync<NotFoundException>(() => CustomerHandler().Handle(new CancelReservationCommand(id), CancellationToken.None));
        Assert.Equal(ReservationStatus.Confirmed, _reservations.Items.Single().Status);
    }

    [Fact]
    public async Task Unknown_reservation_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CustomerHandler().Handle(new CancelReservationCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task An_agent_cancellation_records_who_and_why_releases_the_car_and_voids_the_hold()
    {
        var id = await BookAsync();

        await AgentHandler().Handle(new AgentCancelReservationCommand(id, "Customer called the branch"), CancellationToken.None);

        var reservation = _reservations.Items.Single();
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(CancelledBy.Agent, reservation.Cancellation!.By);
        Assert.Equal("agent-1", reservation.Cancellation.ByUserId);
        Assert.Equal("Customer called the branch", reservation.Cancellation.Reason);
        Assert.All(_inventory.Items, i => Assert.Equal(0, i.Reserved));
        Assert.Equal(PaymentStatus.Voided, _payments.Items.Single().Status);
    }

    [Fact]
    public async Task An_agent_must_give_a_reason_and_nothing_changes_without_one()
    {
        var id = await BookAsync();
        var commitsBefore = _unitOfWork.Commits;

        var ex = await Assert.ThrowsAsync<DomainException>(() => AgentHandler().Handle(new AgentCancelReservationCommand(id, "  "), CancellationToken.None));

        Assert.Equal("reservation.cancel_reason_required", ex.Code);
        Assert.Equal(ReservationStatus.Confirmed, _reservations.Items.Single().Status);
        Assert.Equal(0, _gateway.Voids);
        Assert.Equal(commitsBefore, _unitOfWork.Commits);
    }

    [Fact]
    public async Task A_no_show_captures_one_days_rate_and_releases_the_car()
    {
        var id = await BookAsync();   // 2-day SUV: R1 500 hold
        _clock.Now = Pickup.AddHours(2);

        await NoShowHandler().Handle(new MarkNoShowCommand(id), CancellationToken.None);

        Assert.Equal(ReservationStatus.NoShow, _reservations.Items.Single().Status);
        Assert.Equal(Money.Zar(750m), Assert.Single(_gateway.Captures));
        Assert.All(_inventory.Items, i => Assert.Equal(0, i.Reserved));
    }

    [Fact]
    public async Task A_no_show_cannot_be_recorded_before_pickup_plus_two_hours()
    {
        var id = await BookAsync();
        _clock.Now = Pickup.AddHours(1);

        var ex = await Assert.ThrowsAsync<DomainException>(() => NoShowHandler().Handle(new MarkNoShowCommand(id), CancellationToken.None));

        Assert.Equal("reservation.no_show_too_early", ex.Code);
        Assert.Empty(_gateway.Captures);
    }
}
