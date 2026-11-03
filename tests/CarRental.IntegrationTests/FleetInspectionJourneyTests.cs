using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static CarRental.IntegrationTests.CounterSteps;

namespace CarRental.IntegrationTests;

/// <summary>
/// The whole counter journey over real HTTP, SQL Server, local file storage and the mock gateway:
/// agent registers a car → customer books → checkout inspection with 4 photos → keys handed over →
/// customer views the report and downloads a photo → return inspection with NEW damage → payment
/// captured → the car's history shows the rental, the odometer and the damage. A stranger sees nothing.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class FleetInspectionJourneyTests(ApiFixture fixture)
{
    private const string Durban = "8a1c9b52-4a8e-4f0e-9a51-0b7c1d2e3f03";
    private const string Password = "Test_Pass_123!";

    private sealed record Token(string AccessToken);
    private sealed record Terms(string Version);
    private sealed record CardToken(string Token);
    private sealed record Created(Guid ReservationId);
    private sealed record Vehicle(Guid Id, string Registration, string Status, int OdometerKm);
    private sealed record Payment(string Status, decimal Captured);
    private sealed record Returned(int DistanceKm, Payment Payment);
    private sealed record RentalHistory(Guid ReservationId, int OdometerOut, int? OdometerIn, int? DistanceKm, List<InspectionSummary> Inspections);
    private sealed record InspectionSummary(string Kind, string Status, List<string> NewDamage);
    private sealed record HistoryEvent(string Kind, RentalHistory? Rental);
    private sealed record History(Vehicle Vehicle, List<HistoryEvent> Timeline);
    private sealed record Problem(string Code);

    private static DateTimeOffset DaysAhead(int days)
    {
        var day = DateTime.UtcNow.Date.AddDays(days);
        return new DateTimeOffset(day.Year, day.Month, day.Day, 9, 0, 0, TimeSpan.FromHours(2));
    }

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> api, string email, string password)
    {
        var client = api.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<Token>(Json))!.AccessToken);
        return client;
    }

    private static async Task<(HttpClient Client, string Email)> NewCustomerAsync(WebApplicationFactory<Program> api)
    {
        var email = $"journey-{Guid.NewGuid():N}@example.com";
        (await api.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();
        var client = await LoginAsync(api, email, Password);
        (await client.PutAsJsonAsync("/api/customers/me", new
        {
            firstName = "Journey", lastName = "Driver", phoneNumber = "+27 82 000 0000",
            documentType = "Passport", documentNumber = $"J{Random.Shared.Next(10_000_000, 99_999_999)}", issuingCountry = "GBR",
            dateOfBirth = "1985-02-14", licenceIssuedOn = "2005-03-01",
        }, Json)).EnsureSuccessStatusCode();
        return (client, email);
    }

    [Fact]
    public async Task Register_book_inspect_check_out_report_return_with_damage_capture_and_history()
    {
        var api = fixture.ClockApi;
        fixture.Clock.Now = DateTimeOffset.UtcNow;
        var agent = await LoginAsync(api, "agent@roam.test", ApiFixture.DemoPassword);

        // 1. The agent registers a new SUV at Durban airport.
        var plate = $"ND {Random.Shared.Next(100, 999)}-{Random.Shared.Next(100, 999)}";
        var register = await agent.PostAsJsonAsync("/api/agent/vehicles", new
        {
            registration = plate, vin = $"AHTFR22G{Random.Shared.Next(100_000_000, 999_999_999)}", make = "Toyota", model = "Fortuner",
            year = DateTime.UtcNow.Year - 1, category = "Suv", transmission = "Automatic", homeBranchId = Durban, odometerKm = 15_000,
        }, Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var car = (await register.Content.ReadFromJsonAsync<Vehicle>(Json))!;
        Assert.Equal("Available", car.Status);

        // 2. A customer books an SUV at Durban for 3 days with the approving test card.
        var (customer, email) = await NewCustomerAsync(api);
        var pickup = DaysAhead(50);
        var token = (await (await customer.PostAsJsonAsync("/api/payments/tokenize", new { number = "4242 4242 4242 4242", expiry = "12/34", cvc = "123" }))
            .Content.ReadFromJsonAsync<CardToken>(Json))!.Token;
        var terms = (await customer.GetFromJsonAsync<Terms>("/api/terms/current", Json))!.Version;
        var book = await customer.PostAsJsonAsync("/api/reservations", new
        {
            category = "Suv", pickupBranchId = Durban, returnBranchId = Durban, pickupAt = pickup, returnAt = pickup.AddDays(3),
            acceptedTermsVersion = terms, extras = "None", paymentToken = token,
        }, Json);
        Assert.Equal(HttpStatusCode.Created, book.StatusCode);
        var reservationId = (await book.Content.ReadFromJsonAsync<Created>(Json))!.ReservationId;

        // 3. Pickup day: the agent can't hand over keys before the inspection...
        fixture.Clock.Now = pickup;
        agent = await LoginAsync(api, "agent@roam.test", ApiFixture.DemoPassword);
        var tooSoon = await agent.PostAsJsonAsync("/api/rentals", new { reservationId, vehicleId = car.Id, freeUpgrade = false }, Json);
        Assert.Equal("rental.inspection_required", (await tooSoon.Content.ReadFromJsonAsync<Problem>(Json))!.Code);

        // ...so inspects OUR new car (4 photos) and hands it over.
        var (rentalId, _) = await CheckOutAsync(agent, reservationId, car.Id);

        // 4. The customer sees the completed checkout report and downloads a photo.
        customer = await LoginAsync(api, email, Password);
        var reports = (await customer.GetFromJsonAsync<List<Inspection>>($"/api/reservations/{reservationId}/inspections", Json))!;
        var checkoutReport = Assert.Single(reports);
        Assert.Equal(("Checkout", "Completed", 4), (checkoutReport.Kind, checkoutReport.Status, checkoutReport.Attachments.Count));
        var photoUrl = $"/api/inspections/{checkoutReport.Id}/attachments/{checkoutReport.Attachments[0].Id}";
        var photo = await customer.GetAsync(photoUrl);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType!.MediaType);
        Assert.Contains("nosniff", photo.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(TinyPng, await photo.Content.ReadAsByteArrayAsync());

        // 5. A stranger sees neither the report nor the photo.
        var (stranger, _) = await NewCustomerAsync(api);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/reservations/{reservationId}/inspections")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(photoUrl)).StatusCode);

        // 6. Three days later: return inspection finds a new scratch on the left; the payment is captured.
        fixture.Clock.Now = pickup.AddDays(3);
        agent = await LoginAsync(api, "agent@roam.test", ApiFixture.DemoPassword);
        var returned = await ReturnAsync(agent, reservationId, rentalId, car.OdometerKm + 640,
            new Dictionary<string, (string, string)> { ["ExteriorLeft"] = ("Damaged", "Scratch on the rear left door") });
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        var result = (await returned.Content.ReadFromJsonAsync<Returned>(Json))!;
        Assert.Equal(640, result.DistanceKm);
        Assert.Equal("Captured", result.Payment.Status);

        // 7. The car is back, and its history shows the rental, the odometer readings and the new damage.
        var history = (await agent.GetFromJsonAsync<History>($"/api/agent/vehicles/{car.Id}/history", Json))!;
        Assert.Equal(("Available", 15_640), (history.Vehicle.Status, history.Vehicle.OdometerKm));
        var rental = Assert.Single(history.Timeline, e => e.Rental is not null).Rental!;
        Assert.Equal((reservationId, 15_000, 15_640, 640), (rental.ReservationId, rental.OdometerOut, rental.OdometerIn ?? 0, rental.DistanceKm ?? 0));
        Assert.Contains(rental.Inspections, i => i.Kind == "Return" && i.NewDamage.Count == 1);

        // 8. The customer can now see both reports, the return one listing the new damage.
        customer = await LoginAsync(api, email, Password);
        reports = (await customer.GetFromJsonAsync<List<Inspection>>($"/api/reservations/{reservationId}/inspections", Json))!;
        Assert.Equal(2, reports.Count);
        Assert.Single(reports.Single(r => r.Kind == "Return").NewDamage);

        fixture.Clock.Now = DateTimeOffset.UtcNow;
    }

    [Fact]
    public async Task Uploads_are_checked_by_their_bytes_not_their_name()
    {
        var api = fixture.ClockApi;
        fixture.Clock.Now = DateTimeOffset.UtcNow;
        var agent = await LoginAsync(api, "agent@roam.test", ApiFixture.DemoPassword);
        var (customer, _) = await NewCustomerAsync(api);
        var pickup = DaysAhead(55);
        var token = (await (await customer.PostAsJsonAsync("/api/payments/tokenize", new { number = "4242 4242 4242 4242", expiry = "12/34", cvc = "123" }))
            .Content.ReadFromJsonAsync<CardToken>(Json))!.Token;
        var terms = (await customer.GetFromJsonAsync<Terms>("/api/terms/current", Json))!.Version;
        var reservationId = (await (await customer.PostAsJsonAsync("/api/reservations", new
        {
            category = "Economy", pickupBranchId = Durban, returnBranchId = Durban, pickupAt = pickup, returnAt = pickup.AddDays(1),
            acceptedTermsVersion = terms, extras = "None", paymentToken = token,
        }, Json)).Content.ReadFromJsonAsync<Created>(Json))!.ReservationId;

        var vehicle = (await AvailableVehiclesAsync(agent, reservationId)).First();
        var start = await agent.PostAsJsonAsync("/api/agent/inspections", new { reservationId, kind = "Checkout", vehicleId = vehicle.Id }, Json);
        var report = (await start.Content.ReadFromJsonAsync<Inspection>(Json))!;

        var exe = await UploadAsync(agent, report.Id, [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00], "front.png", "Front");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, exe.StatusCode);
        Assert.Equal("file.type_not_allowed", (await exe.Content.ReadFromJsonAsync<Problem>(Json))!.Code);
    }
}
