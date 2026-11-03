using CarRental.Application.Abstractions.Files;
using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.Payments;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Abstractions.Terms;
using CarRental.Application.Common.Customers;
using CarRental.Application.Common.Exceptions;
using CarRental.Domain.Customers;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using CarRental.Domain.Inventory;
using CarRental.Domain.Payments;
using CarRental.Domain.Pricing;
using CarRental.Domain.Pricing.Rules;
using CarRental.Domain.Rentals;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;

namespace CarRental.Application.Tests.Fakes;

// ---------------------------------------------------------------------------------------------
// FAKES: small, working in-memory implementations of the Application's ports.
// A fake has real behaviour (it stores things); a mock only records calls. Fakes make tests read
// like the real flow and survive refactoring of HOW the handler calls its dependencies.
// ---------------------------------------------------------------------------------------------

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}

internal sealed class FakeCurrentUser(string? userId = "user-1", string? email = "thabo@example.com") : ICurrentUser
{
    public string? UserId { get; set; } = userId;
    public string? Email { get; set; } = email;
    public List<string> RoleList { get; } = new();
    public IReadOnlyList<string> Roles => RoleList;
    public bool IsInRole(string role) => RoleList.Contains(role);
}

internal sealed class InMemoryCustomerRepository : ICustomerRepository
{
    public List<Customer> Items { get; } = new();
    public Task<Customer?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(c => c.UserId == userId));
    public Task<bool> DocumentInUseAsync(IdentityDocument document, CustomerId? exceptCustomer, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(c => c.Document == document && c.Id != exceptCustomer));
    public void Add(Customer customer) => Items.Add(customer);
}

internal sealed class InMemoryBranchRepository(params BranchId[] branches) : IBranchRepository
{
    public Task<bool> ExistsAsync(BranchId id, CancellationToken cancellationToken) => Task.FromResult(branches.Contains(id));
}

internal sealed class FakeTermsProvider : ITermsProvider
{
    public const string Version = "2026-11-01";
    public TermsDocument Current { get; } = new(Version, "Rental terms", new DateOnly(2026, 11, 1), new[] { new TermsSection("Deposit", "A deposit is held.") });
}

/// <summary>A signed-in user with a completed profile: the usual starting point for booking tests.</summary>
internal sealed class SignedInCustomer
{
    public FakeCurrentUser User { get; }
    public InMemoryCustomerRepository Customers { get; } = new();
    public CurrentCustomer Current { get; }
    public Customer Customer { get; }

    public SignedInCustomer(int ageOnPickup = 35, LoyaltyTier tier = LoyaltyTier.None, string userId = "user-1")
    {
        User = new FakeCurrentUser(userId);
        Customer = TestDomain.NewCustomer(userId, ageOnPickup, tier);
        Customers.Add(Customer);
        Current = new CurrentCustomer(User, Customers);
    }

    public SignedInCustomer AddOtherCustomer(string userId, int age = 40)
    {
        Customers.Add(TestDomain.NewCustomer(userId, age, LoyaltyTier.None));
        return this;
    }

    public SignedInCustomer SwitchTo(string userId)
    {
        User.UserId = userId;
        return this;
    }
}

internal sealed class InMemoryReservationRepository : IReservationRepository
{
    public List<Reservation> Items { get; } = new();
    public Task<Reservation?> GetAsync(ReservationId id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(r => r.Id == id));
    public void Add(Reservation reservation) => Items.Add(reservation);
}

internal sealed class InMemoryRentalRepository : IRentalRepository
{
    public List<Rental> Items { get; } = new();
    public Task<Rental?> GetAsync(RentalId id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(r => r.Id == id));
    public Task<Rental?> GetByReservationAsync(ReservationId reservationId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(r => r.ReservationId == reservationId));
    public void Add(Rental rental) => Items.Add(rental);
}

internal sealed class InMemoryVehicleRepository : IVehicleRepository
{
    public List<Vehicle> Items { get; } = new();
    public Task<Vehicle?> GetAsync(VehicleId id, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(v => v.Id == id));
    public Task<bool> RegistrationInUseAsync(Registration registration, CancellationToken cancellationToken) => Task.FromResult(Items.Any(v => v.Registration == registration));
    public Task<bool> VinInUseAsync(Vin vin, CancellationToken cancellationToken) => Task.FromResult(Items.Any(v => v.Vin == vin));
    public void Add(Vehicle vehicle) => Items.Add(vehicle);
}

internal sealed class InMemoryInspectionRepository : IInspectionRepository
{
    public List<InspectionReport> Items { get; } = new();
    public Task<InspectionReport?> GetAsync(InspectionId id, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(i => i.Id == id));
    public Task<InspectionReport?> GetAsync(ReservationId reservationId, InspectionKind kind, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(i => i.ReservationId == reservationId && i.Kind == kind));
    public void Add(InspectionReport report) => Items.Add(report);
}

/// <summary>Files in a dictionary. Records deletes so tests can check no orphan files are left.</summary>
internal sealed class InMemoryFileStorage : IFileStorage
{
    public Dictionary<string, (byte[] Bytes, string ContentType)> Files { get; } = new();

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var key = $"2026/11/{Guid.NewGuid():N}";
        Files[key] = (buffer.ToArray(), contentType);
        return key;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new MemoryStream(Files[storageKey].Bytes));

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        Files.Remove(storageKey);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryPaymentRepository : IPaymentRepository
{
    public List<Payment> Items { get; } = new();
    public Task<Payment?> GetByReservationAsync(ReservationId reservationId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(p => p.ReservationId == reservationId));
    public void Add(Payment payment) => Items.Add(payment);
}

/// <summary>
/// A gateway sandbox in memory: approves "tok_ok", declines "tok_decline" (card_declined), and
/// remembers each idempotency key's first answer, like a real gateway. Counts what it was asked to do.
/// </summary>
internal sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly Dictionary<string, GatewayResult> _byKey = new();

    public List<string> AuthorizeKeys { get; } = new();
    public int Holds { get; private set; }
    public List<Money> Captures { get; } = new();
    public int Voids { get; private set; }
    public bool DeclineCaptures { get; set; }

    public Task<GatewayResult> AuthorizeAsync(string paymentToken, Money amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        AuthorizeKeys.Add(idempotencyKey);
        if (!_byKey.TryGetValue(idempotencyKey, out var result))
        {
            result = paymentToken == "tok_decline"
                ? new GatewayResult(GatewayDecision.Declined, null, "card_declined", "Visa", "0002")
                : new GatewayResult(GatewayDecision.Approved, $"auth_{Guid.NewGuid():N}", null, "Visa", "4242");
            _byKey[idempotencyKey] = result;
            if (result.Approved) Holds++;
        }
        return Task.FromResult(result);
    }

    public Task<GatewayResult> CaptureAsync(string gatewayReference, Money amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        Captures.Add(amount);
        return Task.FromResult(DeclineCaptures
            ? new GatewayResult(GatewayDecision.Declined, null, "card_declined", null, null)
            : new GatewayResult(GatewayDecision.Approved, "capture_1", null, null, null));
    }

    public Task<GatewayResult> VoidAsync(string gatewayReference, string idempotencyKey, CancellationToken cancellationToken)
    {
        Voids++;
        return Task.FromResult(new GatewayResult(GatewayDecision.Approved, "void_1", null, null, null));
    }
}

internal sealed class InMemoryInventoryRepository : ICategoryInventoryRepository
{
    public List<CategoryInventory> Items { get; } = new();

    public InMemoryInventoryRepository Seed(BranchId branch, VehicleCategory category, DateOnly from, int days, int capacity)
    {
        for (var i = 0; i < days; i++)
            Items.Add(CategoryInventory.Create(branch, category, from.AddDays(i), capacity));
        return this;
    }

    public CategoryInventory On(DateOnly date) => Items.Single(i => i.Date == date);

    public Task<IReadOnlyList<CategoryInventory>> GetRangeAsync(BranchId branchId, VehicleCategory category, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CategoryInventory>>(
            Items.Where(i => i.BranchId == branchId && i.Category == category && i.Date >= from && i.Date <= to).ToList());
}

/// <summary>Counts commits; can be told to simulate losing a concurrency race.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Commits { get; private set; }
    public int Discards { get; private set; }
    public int ConflictsToSimulate { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (ConflictsToSimulate > 0)
        {
            ConflictsToSimulate--;
            throw new ConcurrencyConflictException("Simulated: another request changed the row first.");
        }
        Commits++;
        return Task.CompletedTask;
    }

    public void DiscardChanges() => Discards++;
}

internal sealed class FakeAvailabilityReadModel : IAvailabilityReadModel
{
    public List<(Guid BranchId, CategoryDayAvailability Row)> Rows { get; } = new();

    public Task<IReadOnlyList<CategoryDayAvailability>> GetDailyAvailabilityAsync(Guid branchId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CategoryDayAvailability>>(
            Rows.Where(r => r.BranchId == branchId && r.Row.Date >= from && r.Row.Date <= to).Select(r => r.Row).ToList());
}

internal static class TestDomain
{
    public static readonly TimeSpan Sast = TimeSpan.FromHours(2);
    public static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, Sast);
    public static readonly DateTimeOffset Pickup = new(2026, 12, 1, 9, 0, 0, Sast);   // 07:00 UTC
    public static readonly DateOnly PickupDay = new(2026, 12, 1);
    public static readonly BranchId Jnb = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    public static readonly BranchId Cpt = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    public const string ApprovedCard = "tok_ok";
    public const string DeclinedCard = "tok_decline";

    /// <summary>Passport holder (no ID-encoded birth date), so tests can pick any age.</summary>
    public static Customer NewCustomer(string userId, int ageOnPickup, LoyaltyTier tier = LoyaltyTier.None)
    {
        var customer = Customer.Register(userId, "Thabo", "Nkosi", $"{userId}@example.com", "+27 82 459 1102",
            IdentityDocument.Passport($"P{(uint)userId.GetHashCode() % 10_000_000:0000000}", "GBR"),
            PickupDay.AddYears(-ageOnPickup), PickupDay.AddYears(-Math.Min(5, ageOnPickup - 18)), Now);
        customer.SetLoyaltyTier(tier);
        return customer;
    }

    private static int _plate = 100;

    /// <summary>An Available fleet car at <paramref name="branch"/> (JNB by default).</summary>
    public static Vehicle NewVehicle(VehicleCategory category = VehicleCategory.Economy, BranchId? branch = null, int odometerKm = 42_000)
    {
        var n = Interlocked.Increment(ref _plate);
        var vehicle = Vehicle.Register(Registration.Create($"CA {n:000}-{n % 1000:000}"), Vin.Create($"AAVZZZ6RZ{n:00000000}"),
            "Volkswagen", "Polo Vivo", 2025, category, Transmission.Manual, branch ?? Jnb, odometerKm, Now);
        vehicle.ClearDomainEvents();
        return vehicle;
    }

    public static IReadOnlyList<ChecklistItem> AllOk() =>
        Enum.GetValues<ChecklistItemCode>().Select(c => new ChecklistItem(c, ChecklistItemStatus.Ok, null)).ToList();

    /// <summary>A completed Checkout inspection of the car for the booking, with the 4 required photos.</summary>
    public static InspectionReport CompletedCheckout(ReservationId reservationId, VehicleId vehicleId, int odometerKm)
    {
        var report = InspectionReport.Start(reservationId, vehicleId, null, InspectionKind.Checkout, "agent-1", "Thandi", Now);
        report.Update(odometerKm, FuelLevel.Full, AllOk().ToList(), []);
        foreach (var angle in InspectionReport.RequiredCheckoutAngles)
            report.AddAttachment(new InspectionAttachment(AttachmentId.New(), $"{angle}.jpg", "image/jpeg", 100, $"k{Guid.NewGuid():N}", angle, Now, "agent-1"));
        report.Complete(Now);
        return report;
    }

    /// <summary>A completed Return inspection (all Ok unless <paramref name="damage"/> is given), compared with the checkout report.</summary>
    public static InspectionReport CompletedReturn(InspectionReport checkout, RentalId rentalId, int odometerKm, DateTimeOffset now,
        IReadOnlyCollection<DamageMark>? damage = null)
    {
        var report = InspectionReport.Start(checkout.ReservationId, checkout.VehicleId, rentalId, InspectionKind.Return, "agent-1", "Thandi", now);
        report.Update(odometerKm, new FuelLevel(6), AllOk().ToList(), damage ?? []);
        report.Complete(now, checkout);
        return report;
    }

    public static RentalPricer Pricer() => new(
        new IPriceRule[] { new WeeklyDiscountRule(), new LoyaltyDiscountRule(), new YoungDriverFeeRule(), new OneWayFeeRule(), new ExtrasRule() },
        RateCard.Standard);
}
