using CarRental.Domain.Branches;
using CarRental.Domain.Customers;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inventory;
using CarRental.Domain.ValueObjects;
using CarRental.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace CarRental.IntegrationTests;

/// <summary>
/// Starts ONE SQL Server container for the whole test collection (starting one per test would be slow),
/// creates the schema, and hands out fresh DbContexts. Each test uses its own branch so tests never
/// interfere with each other's inventory rows.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public CarRentalDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CarRentalDbContext>().UseSqlServer(_container.GetConnectionString()).Options);

    /// <summary>A brand-new branch with <paramref name="capacity"/> cars per day for <paramref name="days"/> days.</summary>
    public async Task<BranchId> NewBranchWithInventoryAsync(VehicleCategory category, DateOnly from, int days, int capacity)
    {
        var branchId = BranchId.New();
        await using var db = CreateContext();
        db.Branches.Add(Branch.Create(branchId, branchId.Value.ToString("N")[..8], "Test branch", "Test city", BranchType.City, "1 Test St"));
        for (var i = 0; i < days; i++)
            db.Inventory.Add(CategoryInventory.Create(branchId, category, from.AddDays(i), capacity));
        await db.SaveChangesAsync();
        return branchId;
    }

    /// <summary>A registered customer (passport holder, aged 40) for the given user id.</summary>
    public async Task<Customer> NewCustomerAsync(string userId, DateTimeOffset now)
    {
        var customer = Customer.Register(userId, "Test", "Driver", $"{userId}@example.com", "+27 82 000 0000",
            IdentityDocument.Passport($"T{Random.Shared.Next(10_000_000, 99_999_999)}", "GBR"),
            new DateOnly(1986, 1, 1), new DateOnly(2006, 1, 1), now);
        await using var db = CreateContext();
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}

internal sealed class NoOpPublisher : IPublisher
{
    public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification => Task.CompletedTask;
}

internal sealed class FixedUser(string userId) : CarRental.Application.Abstractions.Identity.ICurrentUser
{
    public string? UserId => userId;
    public string? Email => $"{userId}@example.com";
    public IReadOnlyList<string> Roles => Array.Empty<string>();
    public bool IsInRole(string role) => false;
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
