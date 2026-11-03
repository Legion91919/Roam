using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.Payments;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Abstractions.Terms;
using CarRental.Application.Reservations.CreateReservation;
using CarRental.Application.Tests.Fakes;
using CarRental.Domain.Pricing;
using CarRental.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static CarRental.Application.Tests.Fakes.TestDomain;

namespace CarRental.Application.Tests.Behaviors;

/// <summary>
/// Runs commands through the REAL MediatR pipeline and DI registrations (AddApplication), with fake
/// ports. Proves the wiring: handlers are discovered, validators run before handlers, rules are
/// registered. A misconfigured container fails here, not in production.
/// </summary>
public class PipelineWiringTests
{
    private readonly InMemoryInventoryRepository _inventory = new InMemoryInventoryRepository().Seed(Jnb, VehicleCategory.Compact, PickupDay, 10, 2);
    private readonly InMemoryReservationRepository _reservations = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<TimeProvider>(new FixedClock(Now));   // registered first: AddApplication uses TryAdd
        services.AddSingleton<ICategoryInventoryRepository>(_inventory);
        services.AddSingleton<IReservationRepository>(_reservations);
        services.AddSingleton<IPaymentRepository>(new InMemoryPaymentRepository());
        services.AddSingleton<IPaymentGateway>(new FakePaymentGateway());
        services.AddSingleton<IUnitOfWork>(_unitOfWork);
        var signedIn = new SignedInCustomer();
        services.AddSingleton<ICurrentUser>(signedIn.User);
        services.AddSingleton<ICustomerRepository>(signedIn.Customers);
        services.AddSingleton<IBranchRepository>(new InMemoryBranchRepository(Jnb));
        services.AddSingleton<ITermsProvider>(new FakeTermsProvider());
        services.AddApplication();
        return services.BuildServiceProvider();
    }

    private static CreateReservationCommand ValidCommand() => new(VehicleCategory.Compact, Jnb.Value, Jnb.Value,
        Pickup, Pickup.AddDays(2), FakeTermsProvider.Version, PaymentToken: ApprovedCard);

    [Fact]
    public async Task A_valid_command_reaches_its_handler()
    {
        using var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(ValidCommand());

        Assert.Equal(900m, result.Price.Total);
        Assert.Single(_reservations.Items);
    }

    [Fact]
    public async Task An_invalid_command_is_stopped_by_validation_before_the_handler_runs()
    {
        using var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();
        var invalid = ValidCommand() with { PickupBranchId = Guid.Empty, ReturnAt = Pickup.AddHours(-1) };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => sender.Send(invalid));

        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(CreateReservationCommand.PickupBranchId));
        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(CreateReservationCommand.ReturnAt));
        Assert.Empty(_reservations.Items);
        Assert.All(_inventory.Items, i => Assert.Equal(0, i.Reserved));
    }

    [Fact]
    public void All_pricing_rules_are_registered()
    {
        using var provider = BuildProvider();

        Assert.Equal(5, provider.GetServices<IPriceRule>().Count());
    }
}
