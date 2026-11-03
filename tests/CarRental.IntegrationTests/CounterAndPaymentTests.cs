using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CarRental.IntegrationTests;

/// <summary>
/// Payments and the staff counter over real HTTP, real SQL Server and the mock gateway:
/// tokenise a test card → book (authorise) → agent checks out → late return → payment captured.
/// Shares <see cref="ApiFixture"/> (and its containers) with <see cref="ApiEndToEndTests"/>.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class CounterAndPaymentTests(ApiFixture fixture)
{
    private const string Johannesburg = "8a1c9b52-4a8e-4f0e-9a51-0b7c1d2e3f01";
    private const string Password = "Test_Pass_123!";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private sealed record Token(string AccessToken);
    private sealed record Terms(string Version);
    private sealed record CardToken(string Token, string Brand, string Last4);
    private sealed record PaymentDto(string Status, string CardBrand, string CardLast4, decimal Authorized, decimal Captured, string? FailureReason);
    private sealed record Created(Guid ReservationId, PriceDto Price, PaymentDto Payment);
    private sealed record PriceDto(decimal Total);
    private sealed record Details(Guid Id, string Status, PaymentDto? Payment);
    private sealed record RentalDto(Guid RentalId, string Status, bool IsLate);
    private sealed record CounterBooking(Guid ReservationId, string Reference, string Status, string CustomerEmail, PaymentDto? Payment, RentalDto? Rental);
    private sealed record CheckedOut(Guid RentalId);
    private sealed record Returned(int DistanceKm, decimal LateFee, decimal AmountDue, PaymentDto Payment);
    private sealed record Account(string Email, string[] Roles);
    private sealed record Problem(int Status, string Code, string? Reason, Dictionary<string, string[]>? Errors);
    private sealed record Registered(Guid CustomerId, string Email, string TemporaryPassword);
    private sealed record CreatedAtBranch(Guid ReservationId, PaymentDto? Payment, string PaymentOption);
    private sealed record DetailsWithOption(Guid Id, string Status, PaymentDto? Payment, string PaymentOption);

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

    private static Task<HttpClient> AgentAsync(WebApplicationFactory<Program> api) => LoginAsync(api, "agent@roam.test", ApiFixture.DemoPassword);

    /// <summary>A new customer with a passport profile; returns the signed-in client and the email (to sign in again later).</summary>
    private static async Task<(HttpClient Client, string Email)> CustomerAsync(WebApplicationFactory<Program> api)
    {
        var anonymous = api.CreateClient();
        var email = $"driver-{Guid.NewGuid():N}@example.com";
        (await anonymous.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();
        var client = await LoginAsync(api, email, Password);
        (await client.PutAsJsonAsync("/api/customers/me", new
        {
            firstName = "Test", lastName = "Driver", phoneNumber = "+27 82 000 0000",
            documentType = "Passport", documentNumber = $"P{Random.Shared.Next(10_000_000, 99_999_999)}", issuingCountry = "GBR",
            dateOfBirth = "1988-04-20", licenceIssuedOn = "2008-01-15",
        }, Json)).EnsureSuccessStatusCode();
        return (client, email);
    }

    private static async Task<string> TokenizeAsync(HttpClient client, string number)
    {
        var response = await client.PostAsJsonAsync("/api/payments/tokenize", new { number, expiry = "12/34", cvc = "123" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CardToken>(Json))!.Token;
    }

    private static async Task<HttpResponseMessage> BookAsync(HttpClient client, DateTimeOffset pickup, int days, string paymentToken)
    {
        var terms = (await client.GetFromJsonAsync<Terms>("/api/terms/current", Json))!.Version;
        return await client.PostAsJsonAsync("/api/reservations", new
        {
            category = "Economy", pickupBranchId = Johannesburg, returnBranchId = Johannesburg,
            pickupAt = pickup, returnAt = pickup.AddDays(days), acceptedTermsVersion = terms, extras = "None", paymentToken,
        }, Json);
    }

    [Fact]
    public async Task Tokenize_turns_a_test_card_into_a_token_and_rejects_invalid_cards()
    {
        var (client, _) = await CustomerAsync(fixture.SqlReadModelApi);

        var ok = await client.PostAsJsonAsync("/api/payments/tokenize", new { number = "4242 4242 4242 4242", expiry = "12/34", cvc = "123" });
        var card = (await ok.Content.ReadFromJsonAsync<CardToken>(Json))!;
        Assert.Equal(("tok_visa_approve", "Visa", "4242"), (card.Token, card.Brand, card.Last4));

        var bad = await client.PostAsJsonAsync("/api/payments/tokenize", new { number = "4242 4242 4242 4241", expiry = "01/20", cvc = "1" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var problem = (await bad.Content.ReadFromJsonAsync<Problem>(Json))!;
        Assert.Equal("payment.invalid_card", problem.Code);
        Assert.Equal(new[] { "cvc", "expiry", "number" }, problem.Errors!.Keys.Order());
        Assert.DoesNotContain("4242", await bad.Content.ReadAsStringAsync(), StringComparison.Ordinal);   // card data never echoed
    }

    [Fact]
    public async Task A_declined_card_returns_402_payment_declined_and_books_nothing()
    {
        var (client, _) = await CustomerAsync(fixture.SqlReadModelApi);
        var token = await TokenizeAsync(client, "4000 0000 0000 0002");

        var response = await BookAsync(client, DaysAhead(22), 2, token);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<Problem>(Json))!;
        Assert.Equal("payment.declined", problem.Code);
        Assert.Equal("card_declined", problem.Reason);
        Assert.Empty((await client.GetFromJsonAsync<List<Details>>("/api/reservations/mine", Json))!);
    }

    [Fact]
    public async Task Cancelling_voids_the_authorisation()
    {
        var (client, _) = await CustomerAsync(fixture.SqlReadModelApi);
        var created = (await (await BookAsync(client, DaysAhead(23), 2, await TokenizeAsync(client, "5555 5555 5555 4444"))).Content.ReadFromJsonAsync<Created>(Json))!;
        Assert.Equal(("Authorized", "Mastercard", "4444"), (created.Payment.Status, created.Payment.CardBrand, created.Payment.CardLast4));

        await client.PostAsync($"/api/reservations/{created.ReservationId}/cancel", null);

        var details = await client.GetFromJsonAsync<Details>($"/api/reservations/{created.ReservationId}", Json);
        Assert.Equal("Voided", details!.Payment!.Status);
    }

    [Fact]
    public async Task Account_me_tells_customers_from_agents()
    {
        var (customer, email) = await CustomerAsync(fixture.SqlReadModelApi);
        var agent = await AgentAsync(fixture.SqlReadModelApi);

        var customerAccount = await customer.GetFromJsonAsync<Account>("/api/account/me", Json);
        var agentAccount = await agent.GetFromJsonAsync<Account>("/api/account/me", Json);

        Assert.Equal(email, customerAccount!.Email);
        Assert.Empty(customerAccount.Roles);
        Assert.Contains("Agent", agentAccount!.Roles);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.SqlReadModelApi.CreateClient().GetAsync("/api/account/me")).StatusCode);
    }

    [Fact]
    public async Task Staff_endpoints_are_forbidden_to_customers()
    {
        var (customer, _) = await CustomerAsync(fixture.SqlReadModelApi);

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"/api/agent/branches/{Johannesburg}/pickups?date=2026-12-01")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/agent/reservations/ROAM-0000-0000-0000-0000")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync("/api/agent/customers", new { email = "x@example.com" })).StatusCode);
    }

    [Fact]
    public async Task An_agent_finds_any_customers_booking_by_reference_and_sees_it_in_the_pickup_list()
    {
        var (client, email) = await CustomerAsync(fixture.SqlReadModelApi);
        var pickup = DaysAhead(24);
        var created = (await (await BookAsync(client, pickup, 2, "tok_visa_approve")).Content.ReadFromJsonAsync<Created>(Json))!;
        var agent = await AgentAsync(fixture.SqlReadModelApi);
        var reference = "ROAM-" + string.Join('-', created.ReservationId.ToString("N").ToUpperInvariant()[..16].Chunk(4).Select(c => new string(c)));

        var found = await agent.GetFromJsonAsync<CounterBooking>($"/api/agent/reservations/{reference}", Json);
        var pickups = await agent.GetFromJsonAsync<List<CounterBooking>>($"/api/agent/branches/{Johannesburg}/pickups?date={pickup:yyyy-MM-dd}", Json);

        Assert.Equal(created.ReservationId, found!.ReservationId);
        Assert.Equal(email, found.CustomerEmail);
        Assert.Equal("Authorized", found.Payment!.Status);
        Assert.Contains(pickups!, b => b.ReservationId == created.ReservationId);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync("/api/agent/reservations/ROAM-0000-0000-0000-0000")).StatusCode);
    }

    [Fact]
    public async Task An_agent_registers_a_walk_in_customer_who_can_then_sign_in_with_the_temporary_password()
    {
        var agent = await AgentAsync(fixture.SqlReadModelApi);
        var email = $"walkin-{Guid.NewGuid():N}@example.com";
        object WalkIn(string id) => new
        {
            email, firstName = "Lerato", lastName = "Dlamini", phoneNumber = "+27 82 111 2222",
            documentType = "Passport", documentNumber = id, issuingCountry = "GBR",
            dateOfBirth = "1990-05-10", licenceIssuedOn = "2010-06-01",
        };

        var response = await agent.PostAsJsonAsync("/api/agent/customers", WalkIn($"W{Random.Shared.Next(10_000_000, 99_999_999)}"), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registered = (await response.Content.ReadFromJsonAsync<Registered>(Json))!;

        var customer = await LoginAsync(fixture.SqlReadModelApi, email, registered.TemporaryPassword);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/customers/me")).StatusCode);

        var again = await agent.PostAsJsonAsync("/api/agent/customers", WalkIn($"W{Random.Shared.Next(10_000_000, 99_999_999)}"), Json);
        Assert.Equal("account.email_in_use", (await again.Content.ReadFromJsonAsync<Problem>(Json))!.Code);
    }

    [Fact]
    public async Task Book_check_out_and_return_late_captures_the_total_plus_the_late_fee()
    {
        var api = fixture.ClockApi;
        fixture.Clock.Now = DateTimeOffset.UtcNow;
        var (customer, email) = await CustomerAsync(api);
        var pickup = DaysAhead(30);

        // 1. Customer books 12 days with the Visa test card: R3 570 is authorised.
        var created = (await (await BookAsync(customer, pickup, 12, await TokenizeAsync(customer, "4242 4242 4242 4242"))).Content.ReadFromJsonAsync<Created>(Json))!;
        Assert.Equal(3570m, created.Price.Total);
        Assert.Equal("Authorized", created.Payment.Status);

        // 2. Pickup day: the agent hands over the keys.
        fixture.Clock.Now = pickup;
        var agent = await AgentAsync(api);
        var (rentalId, vehicle) = await CounterSteps.CheckOutAsync(agent, created.ReservationId);   // inspection with 4 photos, then keys

        // 3. Two hours after the due time: listed as a late return, returned, late fee captured with the total.
        fixture.Clock.Now = pickup.AddDays(12).AddHours(2);
        agent = await AgentAsync(api);
        var returns = await agent.GetFromJsonAsync<List<CounterBooking>>($"/api/agent/branches/{Johannesburg}/returns?date={pickup.AddDays(12):yyyy-MM-dd}", Json);
        Assert.True(returns!.Single(r => r.ReservationId == created.ReservationId).Rental!.IsLate);

        var returnResponse = await CounterSteps.ReturnAsync(agent, created.ReservationId, rentalId, vehicle.OdometerKm + 1100);
        Assert.Equal(HttpStatusCode.OK, returnResponse.StatusCode);
        var returned = (await returnResponse.Content.ReadFromJsonAsync<Returned>(Json))!;
        Assert.Equal(1100, returned.DistanceKm);
        Assert.Equal(525m, returned.LateFee);
        Assert.Equal(("Captured", 4095m), (returned.Payment.Status, returned.Payment.Captured));

        // 4. The customer sees the captured payment on their booking.
        customer = await LoginAsync(api, email, Password);
        var details = await customer.GetFromJsonAsync<Details>($"/api/reservations/{created.ReservationId}", Json);
        Assert.Equal("CheckedOut", details!.Status);
        Assert.Equal(("Captured", 4095m, "4242"), (details.Payment!.Status, details.Payment.Captured, details.Payment.CardLast4));

        fixture.Clock.Now = DateTimeOffset.UtcNow;
    }

    [Fact]
    public async Task Pay_at_the_branch_books_without_a_card_and_the_agent_takes_the_card_before_the_keys()
    {
        var api = fixture.ClockApi;
        fixture.Clock.Now = DateTimeOffset.UtcNow;
        var (customer, email) = await CustomerAsync(api);
        var pickup = DaysAhead(31);
        var terms = (await customer.GetFromJsonAsync<Terms>("/api/terms/current", Json))!.Version;
        object Booking(string? paymentToken) => new
        {
            category = "Economy", pickupBranchId = Johannesburg, returnBranchId = Johannesburg,
            pickupAt = pickup, returnAt = pickup.AddDays(3), acceptedTermsVersion = terms, extras = "None",
            paymentOption = "PayAtBranch", paymentToken,
        };

        // 1. A pay-at-branch booking must not carry card data; without it the booking is confirmed and nothing is held.
        Assert.Equal(HttpStatusCode.BadRequest, (await customer.PostAsJsonAsync("/api/reservations", Booking("tok_visa_approve"), Json)).StatusCode);
        var response = await customer.PostAsJsonAsync("/api/reservations", Booking(null), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CreatedAtBranch>(Json))!;
        Assert.Null(created.Payment);
        Assert.Equal("PayAtBranch", created.PaymentOption);
        var details = await customer.GetFromJsonAsync<DetailsWithOption>($"/api/reservations/{created.ReservationId}", Json);
        Assert.Equal(("Confirmed", "PayAtBranch"), (details!.Status, details.PaymentOption));
        Assert.Null(details.Payment);

        // 2. Pickup day: inspected, but no keys before the payment.
        fixture.Clock.Now = pickup;
        var agent = await AgentAsync(api);
        var vehicle = (await CounterSteps.AvailableVehiclesAsync(agent, created.ReservationId)).First(v => !v.IsUpgrade);
        await CounterSteps.CompleteInspectionAsync(agent, created.ReservationId, "Checkout", vehicle.Id, vehicle.OdometerKm);
        var tooSoon = await agent.PostAsJsonAsync("/api/rentals", new { reservationId = created.ReservationId, vehicleId = vehicle.Id, freeUpgrade = false }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooSoon.StatusCode);
        Assert.Equal("rental.payment_required", (await tooSoon.Content.ReadFromJsonAsync<Problem>(Json))!.Code);

        // 3. The agent takes the card at the counter: declined first (nothing saved), then a Mastercard.
        var paymentUrl = $"/api/agent/reservations/{created.ReservationId}/payment";
        var declined = await agent.PostAsJsonAsync(paymentUrl, new { paymentToken = await TokenizeAsync(agent, "4000 0000 0000 0002") }, Json);
        Assert.Equal(HttpStatusCode.PaymentRequired, declined.StatusCode);
        Assert.Equal("payment.declined", (await declined.Content.ReadFromJsonAsync<Problem>(Json))!.Code);
        var paid = await agent.PostAsJsonAsync(paymentUrl, new { paymentToken = await TokenizeAsync(agent, "5555 5555 5555 4444") }, Json);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var payment = (await paid.Content.ReadFromJsonAsync<PaymentDto>(Json))!;
        Assert.Equal(("Authorized", "Mastercard", "4444", 1050m), (payment.Status, payment.CardBrand, payment.CardLast4, payment.Authorized));
        var twice = await agent.PostAsJsonAsync(paymentUrl, new { paymentToken = await TokenizeAsync(agent, "4242 4242 4242 4242") }, Json);
        Assert.Equal("payment.already_taken", (await twice.Content.ReadFromJsonAsync<Problem>(Json))!.Code);

        // 4. Now the keys, and the customer sees the hold on their booking.
        var checkout = await agent.PostAsJsonAsync("/api/rentals", new { reservationId = created.ReservationId, vehicleId = vehicle.Id, freeUpgrade = false }, Json);
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        customer = await LoginAsync(api, email, Password);
        details = await customer.GetFromJsonAsync<DetailsWithOption>($"/api/reservations/{created.ReservationId}", Json);
        Assert.Equal(("CheckedOut", "Authorized", "4444"), (details!.Status, details.Payment!.Status, details.Payment.CardLast4));

        // 5. Customers cannot take counter payments.
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync(paymentUrl, new { paymentToken = "tok_visa_approve" }, Json)).StatusCode);

        fixture.Clock.Now = DateTimeOffset.UtcNow;
    }
}
