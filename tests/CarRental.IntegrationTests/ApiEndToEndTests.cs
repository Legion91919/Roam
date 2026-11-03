using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;

namespace CarRental.IntegrationTests;

/// <summary>
/// Hosts the REAL API in memory (WebApplicationFactory) against real SQL Server and MongoDB containers.
/// Covers what unit tests cannot: Program.cs wiring, JSON contracts, HTTP status codes, startup
/// seeding, and the Mongo projection being kept up to date by domain events.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7").Build();

    public const string DemoPassword = "Demo_Pass_123!";

    public WebApplicationFactory<Program> SqlReadModelApi { get; private set; } = null!;
    public WebApplicationFactory<Program> MongoReadModelApi { get; private set; } = null!;

    /// <summary>Same database, but the API's TimeProvider is <see cref="Clock"/>, so a test can move time (checkout, late return).</summary>
    public WebApplicationFactory<Program> ClockApi { get; private set; } = null!;
    public TestClock Clock { get; } = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _mongo.StartAsync());
        SqlReadModelApi = CreateApi("Sql");
        MongoReadModelApi = CreateApi("Mongo");
        ClockApi = CreateApi("Sql", services => services.AddSingleton<TimeProvider>(Clock));
    }

    private WebApplicationFactory<Program> CreateApi(string readModelProvider, Action<IServiceCollection>? testServices = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            if (testServices is not null)
                host.ConfigureTestServices(testServices);
            host.UseEnvironment("Testing");
            host.UseSetting("ConnectionStrings:CarRental", _sql.GetConnectionString());
            host.UseSetting("ConnectionStrings:Mongo", _mongo.GetConnectionString());
            host.UseSetting("ReadModels:Provider", readModelProvider);
            host.UseSetting("Database:InitialiseOnStartup", "true");
            host.UseSetting("Seed:DemoPassword", DemoPassword);   // agent@roam.test for the counter tests
        });

    public async Task DisposeAsync()
    {
        await ClockApi.DisposeAsync();
        await SqlReadModelApi.DisposeAsync();
        await MongoReadModelApi.DisposeAsync();
        await Task.WhenAll(_sql.DisposeAsync().AsTask(), _mongo.DisposeAsync().AsTask());
    }
}

/// <summary>A clock the test moves by hand. Starts at the real time so seeding and "days ahead" still line up.</summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}

/// <summary>Test classes in this collection share ONE ApiFixture (one set of containers) and run one after another.</summary>
[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<ApiFixture>
{
    public const string Name = "API";
}

[Collection(ApiTestGroup.Name)]
public sealed class ApiEndToEndTests(ApiFixture fixture)
{
    private const string Johannesburg = "8a1c9b52-4a8e-4f0e-9a51-0b7c1d2e3f01";
    private const string Durban = "8a1c9b52-4a8e-4f0e-9a51-0b7c1d2e3f03";
    private const string Password = "Test_Pass_123!";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Real "today" + N days at 09:00 SAST: the API uses the system clock and seeds 120 days ahead.</summary>
    private static DateTimeOffset DaysAhead(int days)
    {
        var day = DateTime.UtcNow.Date.AddDays(days);
        return new DateTimeOffset(day.Year, day.Month, day.Day, 9, 0, 0, TimeSpan.FromHours(2));
    }

    private sealed record Token(string AccessToken);
    private sealed record Terms(string Version);
    private sealed record Created(Guid ReservationId, PriceDto Price);
    private sealed record PriceDto(int ChargeableDays, decimal Total, string Currency);
    private sealed record Details(Guid Id, string Category, string Status, string PickupBranchName, decimal Total, string DriverName, string TermsVersion);
    private sealed record Availability(string Category, int AvailableCars, decimal DailyRate);
    private sealed record Problem(int Status, string Code, Dictionary<string, string[]>? Errors);
    private sealed record BranchRow(Guid Id, string Code, string Type);
    private sealed record Profile(string FirstName, string MaskedDocumentNumber, string LoyaltyTier);

    /// <summary>Register + log in through the real Identity endpoints, then save a driver profile.</summary>
    private static async Task<HttpClient> SignedInAsync(WebApplicationFactory<Program> api, string? passportDateOfBirth = "1988-04-20")
    {
        var client = api.CreateClient();
        var email = $"driver-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<Token>(Json))!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        if (passportDateOfBirth is not null)
        {
            var profile = await client.PutAsJsonAsync("/api/customers/me", new
            {
                firstName = "Test", lastName = "Driver", phoneNumber = "+27 82 000 0000",
                documentType = "Passport", documentNumber = $"P{Random.Shared.Next(10_000_000, 99_999_999)}", issuingCountry = "GBR",
                dateOfBirth = passportDateOfBirth, licenceIssuedOn = "2008-01-15",
            }, Json);
            profile.EnsureSuccessStatusCode();
        }
        return client;
    }

    private static async Task<string> CurrentTermsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<Terms>("/api/terms/current", Json))!.Version;

    private static object Booking(string branch, string category, DateTimeOffset pickup, int days, string? terms) => new
    {
        category,
        pickupBranchId = branch,
        returnBranchId = branch,
        pickupAt = pickup,
        returnAt = pickup.AddDays(days),
        acceptedTermsVersion = terms,
        extras = "None",
        paymentToken = "tok_visa_approve",
    };

    [Fact]
    public async Task Branches_include_airports_and_city_branches()
    {
        var branches = await fixture.SqlReadModelApi.CreateClient().GetFromJsonAsync<List<BranchRow>>("/api/branches", Json);

        Assert.Contains(branches!, b => b.Type == "Airport");
        Assert.Contains(branches!, b => b.Type == "City");
    }

    [Fact]
    public async Task Anonymous_users_cannot_book()
    {
        var response = await fixture.SqlReadModelApi.CreateClient().PostAsJsonAsync("/api/reservations", Booking(Johannesburg, "Compact", DaysAhead(20), 3, "x"), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Book_read_list_and_cancel_through_http()
    {
        var client = await SignedInAsync(fixture.SqlReadModelApi);
        var terms = await CurrentTermsAsync(client);

        var create = await client.PostAsJsonAsync("/api/reservations", Booking(Johannesburg, "Compact", DaysAhead(20), 3, terms), Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<Created>(Json))!;
        Assert.Equal(3 * 450m, created.Price.Total);

        var details = await client.GetFromJsonAsync<Details>($"/api/reservations/{created.ReservationId}", Json);
        Assert.Equal("Confirmed", details!.Status);
        Assert.Equal("Test Driver", details.DriverName);
        Assert.Equal(terms, details.TermsVersion);

        var mine = await client.GetFromJsonAsync<List<Details>>("/api/reservations/mine", Json);
        Assert.Contains(mine!, r => r.Id == created.ReservationId);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/reservations/{created.ReservationId}/cancel", null)).StatusCode);
        var secondCancel = await client.PostAsync($"/api/reservations/{created.ReservationId}/cancel", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondCancel.StatusCode);
        Assert.Equal("reservation.not_cancellable", (await secondCancel.Content.ReadFromJsonAsync<Problem>(Json))!.Code);
    }

    [Fact]
    public async Task Another_customer_cannot_see_or_cancel_my_reservation()
    {
        var me = await SignedInAsync(fixture.SqlReadModelApi);
        var stranger = await SignedInAsync(fixture.SqlReadModelApi);
        var created = (await (await me.PostAsJsonAsync("/api/reservations", Booking(Johannesburg, "Economy", DaysAhead(25), 1, await CurrentTermsAsync(me)), Json))
            .Content.ReadFromJsonAsync<Created>(Json))!;

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/reservations/{created.ReservationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/reservations/{created.ReservationId}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Booking_without_accepting_the_terms_returns_422()
    {
        var client = await SignedInAsync(fixture.SqlReadModelApi);

        var response = await client.PostAsJsonAsync("/api/reservations", Booking(Johannesburg, "Economy", DaysAhead(20), 1, null), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("terms.not_accepted", (await response.Content.ReadFromJsonAsync<Problem>(Json))!.Code);
    }

    [Fact]
    public async Task Booking_without_a_profile_returns_422_profile_required()
    {
        var client = await SignedInAsync(fixture.SqlReadModelApi, passportDateOfBirth: null);

        var response = await client.PostAsJsonAsync("/api/reservations", Booking(Johannesburg, "Economy", DaysAhead(20), 1, await CurrentTermsAsync(client)), Json);

        Assert.Equal("customer.profile_required", (await response.Content.ReadFromJsonAsync<Problem>(Json))!.Code);
    }

    [Fact]
    public async Task SA_ID_number_must_match_the_date_of_birth_and_is_returned_masked()
    {
        var client = await SignedInAsync(fixture.SqlReadModelApi, passportDateOfBirth: null);
        object Profile(string dob) => new
        {
            firstName = "Lerato", lastName = "Dlamini", phoneNumber = "+27 82 111 2222", documentType = "SouthAfricanId",
            documentNumber = "9510151234084", dateOfBirth = dob, licenceIssuedOn = "2015-01-10",
        };

        var mismatch = await client.PutAsJsonAsync("/api/customers/me", Profile("1990-01-01"), Json);
        Assert.Equal("customer.birth_date_mismatch", (await mismatch.Content.ReadFromJsonAsync<Problem>(Json))!.Code);

        var ok = await client.PutAsJsonAsync("/api/customers/me", Profile("1995-10-15"), Json);
        var profile = (await ok.Content.ReadFromJsonAsync<Profile>(Json))!;
        Assert.Equal("•••••••••4084", profile.MaskedDocumentNumber);   // never the full ID number
        Assert.Equal("None", profile.LoyaltyTier);
    }

    [Fact]
    public async Task Business_rule_violations_return_422_with_a_code()
    {
        var underAge = DateTime.UtcNow.AddYears(-19).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var client = await SignedInAsync(fixture.SqlReadModelApi, passportDateOfBirth: underAge);

        var response = await client.PostAsJsonAsync("/api/reservations", Booking(Johannesburg, "Economy", DaysAhead(20), 1, await CurrentTermsAsync(client)), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("eligibility.under_age", (await response.Content.ReadFromJsonAsync<Problem>(Json))!.Code);
    }

    [Fact]
    public async Task Malformed_requests_return_400_with_field_errors()
    {
        var client = await SignedInAsync(fixture.SqlReadModelApi);
        var pickup = DaysAhead(20);
        var invalid = new { category = "Economy", pickupBranchId = Guid.Empty, returnBranchId = Johannesburg, pickupAt = pickup, returnAt = pickup.AddHours(-1), acceptedTermsVersion = "x" };

        var response = await client.PostAsJsonAsync("/api/reservations", invalid, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<Problem>(Json))!;
        Assert.Equal("validation.failed", problem.Code);
        Assert.Contains("PickupBranchId", problem.Errors!.Keys);
    }

    [Fact]
    public async Task Selling_the_last_car_returns_409_sold_out()
    {
        var client = await SignedInAsync(fixture.SqlReadModelApi);
        var terms = await CurrentTermsAsync(client);
        var pickup = DaysAhead(30);
        // Capacity follows the seeded fleet (B33), so ask the API how many Luxury cars JNB has that day.
        var search = $"/api/availability?branchId={Johannesburg}&pickupAt={Uri.EscapeDataString(pickup.ToString("o"))}&returnAt={Uri.EscapeDataString(pickup.AddDays(1).ToString("o"))}";
        var left = (await client.GetFromJsonAsync<List<Availability>>(search, Json))!.Single(a => a.Category == "Luxury").AvailableCars;
        Assert.InRange(left, 1, 10);

        for (var i = 0; i < left; i++)
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/reservations", Booking(Johannesburg, "Luxury", pickup, 1, terms), Json)).StatusCode);
        var oneMore = await client.PostAsJsonAsync("/api/reservations", Booking(Johannesburg, "Luxury", pickup, 1, terms), Json);

        Assert.Equal(HttpStatusCode.Conflict, oneMore.StatusCode);
        Assert.Equal("inventory.sold_out", (await oneMore.Content.ReadFromJsonAsync<Problem>(Json))!.Code);
    }

    [Fact]
    public async Task Counter_operations_are_for_agents_only()
    {
        var customer = await SignedInAsync(fixture.SqlReadModelApi);

        var response = await customer.PostAsJsonAsync("/api/rentals", new { reservationId = Guid.NewGuid(), vehicleRegistration = "CA 1", odometerKm = 10 }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Mongo_read_model_is_updated_by_domain_events()
    {
        var client = await SignedInAsync(fixture.MongoReadModelApi);   // startup rebuilds the projection from SQL
        var terms = await CurrentTermsAsync(client);
        var pickup = DaysAhead(40);
        var search = $"/api/availability?branchId={Durban}&pickupAt={Uri.EscapeDataString(pickup.ToString("o"))}&returnAt={Uri.EscapeDataString(pickup.AddDays(2).ToString("o"))}";

        async Task<int> SuvsLeft() =>
            (await client.GetFromJsonAsync<List<Availability>>(search, Json))!.Single(a => a.Category == "Suv").AvailableCars;

        var before = await SuvsLeft();
        var create = await client.PostAsJsonAsync("/api/reservations", Booking(Durban, "Suv", pickup, 2, terms), Json);
        var created = (await create.Content.ReadFromJsonAsync<Created>(Json))!;
        var afterBooking = await SuvsLeft();
        await client.PostAsync($"/api/reservations/{created.ReservationId}/cancel", null);
        var afterCancel = await SuvsLeft();

        Assert.Equal(4, before);
        Assert.Equal(before - 1, afterBooking);   // ReservationCreated -> $inc Reserved in Mongo
        Assert.Equal(before, afterCancel);        // ReservationCancelled -> $inc -1
    }
}
