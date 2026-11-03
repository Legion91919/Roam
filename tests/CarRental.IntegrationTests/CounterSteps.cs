using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarRental.IntegrationTests;

/// <summary>
/// The counter workflow over HTTP, as the staff app drives it: pick an available car, complete the
/// inspection (checklist, odometer, fuel, the 4 required photos), then hand over or take back the keys.
/// </summary>
internal static class CounterSteps
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>A real 1x1 PNG: passes the magic-byte check and renders in a browser.</summary>
    public static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    public static readonly string[] ChecklistCodes =
    {
        "ExteriorFront", "ExteriorRear", "ExteriorLeft", "ExteriorRight", "Roof", "Windscreen", "WheelsAndTyres",
        "SpareWheelAndJack", "InteriorAndSeats", "WarningLights", "LicenceDiscValid", "DriverIdAndLicenceVerified",
    };

    public sealed record AvailableVehicle(Guid Id, string Registration, string Category, int OdometerKm, bool IsUpgrade);
    public sealed record Attachment(Guid Id, string FileName, string ContentType, long SizeBytes, string Angle);
    public sealed record NewDamage(string? Item, string Panel, string Description, string? Severity);
    public sealed record Inspection(Guid Id, Guid ReservationId, Guid? RentalId, Guid VehicleId, string Kind, string Status,
        int? OdometerKm, int? FuelEighths, List<Attachment> Attachments, List<NewDamage> NewDamage);
    public sealed record CheckedOut(Guid RentalId);

    public static async Task<List<AvailableVehicle>> AvailableVehiclesAsync(HttpClient agent, Guid reservationId) =>
        (await agent.GetFromJsonAsync<List<AvailableVehicle>>($"/api/agent/vehicles/available-for/{reservationId}", Json))!;

    /// <summary>Start, fill (all Ok unless overridden), photograph and complete an inspection. Returns the completed report.</summary>
    public static async Task<Inspection> CompleteInspectionAsync(HttpClient agent, Guid reservationId, string kind, Guid? vehicleId, int odometerKm,
        IReadOnlyDictionary<string, (string Status, string Note)>? overrides = null)
    {
        var start = await agent.PostAsJsonAsync("/api/agent/inspections", new { reservationId, kind, vehicleId }, Json);
        await EnsureOk(start);
        var report = (await start.Content.ReadFromJsonAsync<Inspection>(Json))!;

        var checklist = ChecklistCodes.Select(code => overrides is not null && overrides.TryGetValue(code, out var o)
            ? new { code, status = o.Status, note = (string?)o.Note }
            : new { code, status = "Ok", note = (string?)null }).ToList();
        await EnsureOk(await agent.PutAsJsonAsync($"/api/agent/inspections/{report.Id}", new { odometerKm, fuelEighths = 8, checklist, damage = Array.Empty<object>() }, Json));

        if (kind == "Checkout")
            foreach (var angle in new[] { "Front", "Back", "Left", "Right" })
                await EnsureOk(await UploadAsync(agent, report.Id, TinyPng, $"{angle.ToLowerInvariant()}.png", angle));

        var complete = await agent.PostAsync($"/api/agent/inspections/{report.Id}/complete", null);
        await EnsureOk(complete);
        return (await complete.Content.ReadFromJsonAsync<Inspection>(Json))!;
    }

    public static Task<HttpResponseMessage> UploadAsync(HttpClient agent, Guid inspectionId, byte[] bytes, string fileName, string angle)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", fileName);
        form.Add(new StringContent(angle), "angle");
        return agent.PostAsync($"/api/agent/inspections/{inspectionId}/attachments", form);
    }

    /// <summary>Pick the first available car of the booked category, inspect it and hand over the keys. Returns the rental id and car.</summary>
    public static async Task<(Guid RentalId, AvailableVehicle Vehicle)> CheckOutAsync(HttpClient agent, Guid reservationId, Guid? vehicleId = null)
    {
        var vehicles = await AvailableVehiclesAsync(agent, reservationId);
        var vehicle = vehicleId is null ? vehicles.First(v => !v.IsUpgrade) : vehicles.Single(v => v.Id == vehicleId);
        await CompleteInspectionAsync(agent, reservationId, "Checkout", vehicle.Id, vehicle.OdometerKm);

        var checkout = await agent.PostAsJsonAsync("/api/rentals", new { reservationId, vehicleId = vehicle.Id, freeUpgrade = false }, Json);
        await EnsureOk(checkout);
        return ((await checkout.Content.ReadFromJsonAsync<CheckedOut>(Json))!.RentalId, vehicle);
    }

    /// <summary>Complete the return inspection with the given odometer, then close the rental.</summary>
    public static async Task<HttpResponseMessage> ReturnAsync(HttpClient agent, Guid reservationId, Guid rentalId, int odometerKm,
        IReadOnlyDictionary<string, (string Status, string Note)>? damage = null)
    {
        await CompleteInspectionAsync(agent, reservationId, "Return", null, odometerKm, damage);
        return await agent.PostAsync($"/api/rentals/{rentalId}/return", null);
    }

    private static async Task EnsureOk(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)response.StatusCode} {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}: {await response.Content.ReadAsStringAsync()}");
    }
}
