using CarRental.Domain.Branches;
using CarRental.Domain.Customers;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inventory;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;
using CarRental.Infrastructure.Identity;
using CarRental.Infrastructure.ReadModels.Mongo;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CarRental.Infrastructure.Persistence.Seed;

/// <summary>
/// Development bootstrap: create the schemas, seed branches, keep a rolling window of daily
/// inventory rows ("fleet planning"), and (only when Seed:DemoPassword is configured) create demo
/// accounts. Production would apply EF migrations in the pipeline instead (README: "Migrations")
/// and run inventory planning as a scheduled job.
/// </summary>
public static class DatabaseInitializer
{
    public static readonly BranchId JohannesburgAirport = new(Guid.Parse("8a1c9b52-4a8e-4f0e-9a51-0b7c1d2e3f01"));
    public static readonly BranchId CapeTownAirport = new(Guid.Parse("8a1c9b52-4a8e-4f0e-9a51-0b7c1d2e3f02"));
    public static readonly BranchId DurbanKingShaka = new(Guid.Parse("8a1c9b52-4a8e-4f0e-9a51-0b7c1d2e3f03"));

    private const int PlanningWindowDays = 120;

    /// <summary>A fresh set each call: entity instances must not be shared between DbContexts.</summary>
    private static Branch[] CreateBranches() => new[]
    {
        Branch.Create(JohannesburgAirport, "JNB", "O.R. Tambo International Airport", "Johannesburg", BranchType.Airport, "Terminal A, Car Rental Level 0, Kempton Park"),
        Branch.Create(Id(11), "SDT", "Sandton City", "Johannesburg", BranchType.City, "Rivonia Rd & 5th St, Sandton"),
        Branch.Create(Id(12), "RSB", "Rosebank", "Johannesburg", BranchType.City, "Oxford Rd, Rosebank"),
        Branch.Create(Id(13), "PTA", "Menlyn", "Pretoria", BranchType.City, "Atterbury Rd, Menlyn"),
        Branch.Create(CapeTownAirport, "CPT", "Cape Town International Airport", "Cape Town", BranchType.Airport, "Central Terminal, Car Rental Parkade"),
        Branch.Create(Id(21), "CTC", "Cape Town CBD", "Cape Town", BranchType.City, "Strand St, Cape Town City Centre"),
        Branch.Create(Id(22), "WFT", "V&A Waterfront", "Cape Town", BranchType.City, "Dock Rd, V&A Waterfront"),
        Branch.Create(DurbanKingShaka, "DUR", "King Shaka International Airport", "Durban", BranchType.Airport, "Arrivals Hall, La Mercy"),
        Branch.Create(Id(31), "UMH", "Umhlanga", "Durban", BranchType.City, "Lighthouse Rd, Umhlanga Rocks"),
        Branch.Create(Id(41), "PLZ", "Chief Dawid Stuurman International Airport", "Gqeberha", BranchType.Airport, "Allister Miller Dr, Walmer"),
    };

    /// <summary>Seeded fleet size per category (3 to 8 cars). Inventory capacity is then counted from the fleet.</summary>
    private static readonly Dictionary<VehicleCategory, int> AirportFleet = new()
    {
        [VehicleCategory.Economy] = 8, [VehicleCategory.Compact] = 6, [VehicleCategory.Suv] = 4, [VehicleCategory.Luxury] = 3,
    };

    private static readonly Dictionary<VehicleCategory, int> CityFleet = new()
    {
        [VehicleCategory.Economy] = 4, [VehicleCategory.Compact] = 3, [VehicleCategory.Suv] = 3, [VehicleCategory.Luxury] = 3,
    };

    private static readonly Dictionary<VehicleCategory, (string Make, string Model, Transmission Gearbox)[]> Models = new()
    {
        [VehicleCategory.Economy] = [("Volkswagen", "Polo Vivo", Transmission.Manual), ("Suzuki", "Swift", Transmission.Manual), ("Toyota", "Starlet", Transmission.Manual)],
        [VehicleCategory.Compact] = [("Toyota", "Corolla Cross", Transmission.Automatic), ("Volkswagen", "Polo", Transmission.Automatic), ("Hyundai", "i20", Transmission.Manual)],
        [VehicleCategory.Suv] = [("Toyota", "Fortuner", Transmission.Automatic), ("Hyundai", "Tucson", Transmission.Automatic), ("Volkswagen", "Tiguan", Transmission.Automatic)],
        [VehicleCategory.Luxury] = [("Mercedes-Benz", "C-Class", Transmission.Automatic), ("BMW", "3 Series", Transmission.Automatic), ("Audi", "A4", Transmission.Automatic)],
    };

    /// <summary>World manufacturer prefixes (VW SA, Toyota SA, Mercedes-Benz SA…) for realistic-looking VINs.</summary>
    private static readonly Dictionary<string, string> Wmi = new()
    {
        ["Volkswagen"] = "AAV", ["Toyota"] = "AHT", ["Suzuki"] = "MA3", ["Hyundai"] = "KMH", ["Mercedes-Benz"] = "AAM", ["BMW"] = "WBA", ["Audi"] = "WAU",
    };

    private static BranchId Id(int n) => new(Guid.Parse($"8a1c9b52-4a8e-4f0e-9a51-0b7c1d2e{n:0000}"));

    public static async Task InitialiseAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<CarRentalDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        var clock = provider.GetRequiredService<TimeProvider>();

        await WaitForDatabaseAsync(db, logger, cancellationToken);
        await RecreateIfSchemaOutdatedAsync(db, logger, cancellationToken);
        await EnsureSchemaTablesAsync(provider.GetRequiredService<AuthDbContext>(), AuthDbContext.Schema, cancellationToken);
        await SeedAsync(db, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), cancellationToken, clock.GetUtcNow());

        var demoPassword = provider.GetRequiredService<IConfiguration>()["Seed:DemoPassword"];
        if (!string.IsNullOrWhiteSpace(demoPassword))
            await SeedDemoAccountsAsync(provider, db, demoPassword, clock.GetUtcNow(), logger, cancellationToken);

        var rebuilder = provider.GetService<AvailabilityProjectionRebuilder>();
        if (rebuilder is not null)
        {
            await rebuilder.RebuildAsync(cancellationToken);
            logger.LogInformation("MongoDB availability projection rebuilt from SQL");
        }
    }

    /// <summary>SQL Server in Docker takes a few seconds to accept connections; retry instead of crashing.</summary>
    private static async Task WaitForDatabaseAsync(CarRentalDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.EnsureCreatedAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < 10 && ex is not OperationCanceledException)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    /// <summary>
    /// EnsureCreated never changes an EXISTING database, so a developer's Docker volume from before a
    /// new table (e.g. Payments) or column (e.g. Reservations.PaymentOption) would make every request
    /// fail. Development answer: if a table or column the model expects is missing, drop and recreate
    /// the database and re-seed the demo data.
    /// Production answer: EF Core migrations (README "Migrations"); this method never runs there.
    /// </summary>
    private static async Task RecreateIfSchemaOutdatedAsync(CarRentalDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var expected = db.Model.GetEntityTypes()
            .Where(t => !t.IsOwned() && t.GetTableName() is not null)
            .Select(t => t.GetTableName()!)
            .Distinct()
            .ToList();
        var existing = await db.Database
            .SqlQuery<string>($"SELECT name AS [Value] FROM sys.tables WHERE SCHEMA_NAME(schema_id) = {CarRentalDbContext.Schema}")
            .ToListAsync(cancellationToken);

        var missing = expected.Except(existing, StringComparer.OrdinalIgnoreCase).ToList();

        // A new COLUMN on an existing table (e.g. Reservations.PaymentOption) breaks queries just the same.
        if (missing.Count == 0)
        {
            var expectedColumns = db.Model.GetEntityTypes()
                .Where(t => !t.IsOwned() && t.GetTableName() is not null)
                .SelectMany(t => t.GetDeclaredProperties().Select(p => $"{t.GetTableName()}.{p.GetColumnName()}"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var existingColumns = await db.Database
                .SqlQuery<string>($"SELECT TABLE_NAME + '.' + COLUMN_NAME AS [Value] FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = {CarRentalDbContext.Schema}")
                .ToListAsync(cancellationToken);
            missing = expectedColumns.Except(existingColumns, StringComparer.OrdinalIgnoreCase).ToList();
        }
        if (missing.Count == 0)
            return;

        logger.LogWarning("Development database is missing {Objects}: recreating it and re-seeding demo data", string.Join(", ", missing));
        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    /// <summary>
    /// EnsureCreated only acts when the DATABASE is missing, so a second DbContext sharing the
    /// database (Identity) would never get its tables. Create them when its schema is empty.
    /// (Migrations remove the need for this.)
    /// </summary>
    public static async Task EnsureSchemaTablesAsync(DbContext context, string schema, CancellationToken cancellationToken)
    {
        var tables = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE SCHEMA_NAME(schema_id) = {schema}")
            .SingleAsync(cancellationToken);

        if (tables == 0)
            await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(cancellationToken);
    }

    public static async Task SeedAsync(CarRentalDbContext db, DateOnly today, CancellationToken cancellationToken, DateTimeOffset? now = null)
    {
        var existingCodes = await db.Branches.Select(b => b.Code).ToListAsync(cancellationToken);
        db.Branches.AddRange(CreateBranches().Where(b => !existingCodes.Contains(b.Code)));
        await db.SaveChangesAsync(cancellationToken);

        var branches = await db.Branches.AsNoTracking().Select(b => new { b.Id, b.Code, b.City, b.Type }).ToListAsync(cancellationToken);
        await SeedFleetAsync(db, branches.Select(b => (b.Id, b.Code, b.City, b.Type)).ToList(), now ?? DateTimeOffset.UtcNow, cancellationToken);

        // The fleet drives inventory: capacity per (branch, category) = cars in service there (not retired).
        var fleet = (await db.Vehicles.AsNoTracking()
                .Where(v => v.Status != VehicleStatus.Retired)
                .GroupBy(v => new { v.CurrentBranchId, v.Category })
                .Select(g => new { g.Key.CurrentBranchId, g.Key.Category, Cars = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => (x.CurrentBranchId, x.Category), x => x.Cars);

        // Top up the rolling planning window: add any missing (branch, category, day) rows.
        var from = today.AddDays(-1);
        var to = today.AddDays(PlanningWindowDays);
        var existing = (await db.Inventory.AsNoTracking()
                .Where(i => i.Date >= from && i.Date <= to)
                .Select(i => new { i.BranchId, i.Category, i.Date })
                .ToListAsync(cancellationToken))
            .Select(i => (i.BranchId, i.Category, i.Date))
            .ToHashSet();

        foreach (var branch in branches)
        foreach (var category in Enum.GetValues<VehicleCategory>())
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (!existing.Contains((branch.Id, category, day)))
                db.Inventory.Add(CategoryInventory.Create(branch.Id, category, day, fleet.GetValueOrDefault((branch.Id, category))));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 3 to 8 cars per branch and category with realistic South African plates for the province
    /// (Gauteng "AB 12 CD GP", Western Cape "CA 123-456", KZN "ND 123-456", Eastern Cape "HXY 123 EC")
    /// and VINs. Only for (branch, category) pairs that have no cars yet. Added directly (not through
    /// the use case) because the inventory below is created from the fleet size.
    /// </summary>
    private static async Task SeedFleetAsync(CarRentalDbContext db, List<(BranchId Id, string Code, string City, BranchType Type)> branches, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var stocked = (await db.Vehicles.AsNoTracking().Select(v => new { v.HomeBranchId, v.Category }).Distinct().ToListAsync(cancellationToken))
            .Select(x => (x.HomeBranchId, x.Category)).ToHashSet();

        for (var b = 0; b < branches.Count; b++)
        {
            var branch = branches[b];
            foreach (var (category, count) in branch.Type == BranchType.Airport ? AirportFleet : CityFleet)
            {
                if (stocked.Contains((branch.Id, category))) continue;
                for (var n = 0; n < count; n++)
                {
                    var serial = (b * 100) + ((int)category * 10) + n + 1;   // unique per seeded car
                    var (make, model, gearbox) = Models[category][n % Models[category].Length];
                    var year = now.Year - (n % 4);
                    var vehicle = Vehicle.Register(
                        Registration.Create(PlateFor(branch.City, branch.Code, serial)),
                        Vin.Create($"{Wmi[make]}ZZZ{(int)category}{n}{VinYear(year)}{serial:000000}".PadRight(17, '0')[..17]),
                        make, model, year, category, gearbox, branch.Id, 4_000 + (serial * 1_731 % 60_000), now);
                    vehicle.ClearDomainEvents();   // capacity is counted from the fleet below, not incremented per event
                    db.Vehicles.Add(vehicle);
                }
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string PlateFor(string city, string code, int serial) => city switch
    {
        "Cape Town" => $"CA {serial:000}-{(serial * 37) % 1000:000}",
        "Durban" => $"ND {serial:000}-{(serial * 53) % 1000:000}",
        "Gqeberha" => $"H{(char)('A' + serial % 26)}{(char)('B' + serial % 24)} {serial:000} EC",
        _ => $"{(char)('B' + serial % 24)}{(char)('C' + serial % 23)} {serial % 100:00} {code[..2]} GP",
    };

    /// <summary>VIN position 10 encodes the model year (2020 = L ... 2026 = T; I, O, Q, U, Z skipped).</summary>
    private static char VinYear(int year) => "ABCDEFGHJKLMNPRSTVWXY123456789"[(year - 2010) % 30];

    /// <summary>
    /// DEVELOPMENT ONLY: a counter agent, a Gold-tier customer and an under-25 customer (young driver
    /// fee), so a reviewer can try every role and pricing rule.
    /// Runs only when Seed:DemoPassword is set (it is in appsettings.Development.json, never in prod).
    /// </summary>
    private static async Task SeedDemoAccountsAsync(IServiceProvider provider, CarRentalDbContext db, string password, DateTimeOffset now, ILogger logger, CancellationToken cancellationToken)
    {
        var users = provider.GetRequiredService<UserManager<IdentityUser>>();
        var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roles.RoleExistsAsync(Roles.Agent))
            await roles.CreateAsync(new IdentityRole(Roles.Agent));

        var agent = await EnsureUserAsync(users, "agent@roam.test", password);
        if (agent is not null && !await users.IsInRoleAsync(agent, Roles.Agent))
            await users.AddToRoleAsync(agent, Roles.Agent);

        var gold = await EnsureUserAsync(users, "gold@roam.test", password);
        if (gold is not null && !await db.Customers.AnyAsync(c => c.UserId == gold.Id, cancellationToken))
        {
            var customer = Customer.Register(gold.Id, "Naledi", "Mokoena", "gold@roam.test", "+27 82 555 0101",
                IdentityDocument.SouthAfricanId("8512310001082", DateOnly.FromDateTime(now.UtcDateTime)),
                new DateOnly(1985, 12, 31), new DateOnly(2005, 3, 1), now);
            customer.SetLoyaltyTier(LoyaltyTier.Gold);
            db.Customers.Add(customer);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Born 2003-08-12 (encoded in the SA ID number): under 25, so bookings show the young driver fee.
        var young = await EnsureUserAsync(users, "young@roam.test", password);
        if (young is not null && !await db.Customers.AnyAsync(c => c.UserId == young.Id, cancellationToken))
        {
            db.Customers.Add(Customer.Register(young.Id, "Sipho", "Ndlovu", "young@roam.test", "+27 82 555 0202",
                IdentityDocument.SouthAfricanId("0308125800083", DateOnly.FromDateTime(now.UtcDateTime)),
                new DateOnly(2003, 8, 12), new DateOnly(2022, 1, 10), now));
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Demo accounts ready: agent@roam.test (Agent), gold@roam.test (Gold customer), young@roam.test (under 25)");
    }

    private static async Task<IdentityUser?> EnsureUserAsync(UserManager<IdentityUser> users, string email, string password)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is not null) return user;

        user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, password);
        return result.Succeeded ? user : null;
    }
}
