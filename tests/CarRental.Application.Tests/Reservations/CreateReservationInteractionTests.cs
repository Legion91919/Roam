using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Reservations.CreateReservation;
using CarRental.Application.Tests.Fakes;
using CarRental.Domain.Common;
using CarRental.Domain.ValueObjects;
using NSubstitute;
using static CarRental.Application.Tests.Fakes.TestDomain;

namespace CarRental.Application.Tests.Reservations;

/// <summary>
/// MOCKS (NSubstitute) where the thing we care about is an INTERACTION, not state:
/// "was the unit of work committed exactly once?" / "was it never committed?".
/// Prefer fakes for state, mocks for verifying calls at a boundary.
/// </summary>
public class CreateReservationInteractionTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly InMemoryInventoryRepository _inventory = new();

    private CreateReservationHandler Handler() =>
        new(new SignedInCustomer().Current, new InMemoryBranchRepository(Jnb), _inventory, new InMemoryReservationRepository(), new InMemoryPaymentRepository(), new FakePaymentGateway(), _unitOfWork, new FakeTermsProvider(), Pricer(), new FixedClock(Now));

    private static CreateReservationCommand Command() => new(VehicleCategory.Economy, Jnb.Value, Jnb.Value,
        Pickup, Pickup.AddDays(1), FakeTermsProvider.Version, PaymentToken: ApprovedCard);

    [Fact]
    public async Task Commits_exactly_once_per_booking()
    {
        _inventory.Seed(Jnb, VehicleCategory.Economy, PickupDay, 5, 1);

        await Handler().Handle(Command(), CancellationToken.None);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Never_commits_when_sold_out()
    {
        _inventory.Seed(Jnb, VehicleCategory.Economy, PickupDay, 5, 0);

        await Assert.ThrowsAsync<CapacityExceededException>(() => Handler().Handle(Command(), CancellationToken.None));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
