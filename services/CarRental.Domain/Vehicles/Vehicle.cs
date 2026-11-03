using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.ValueObjects;

namespace CarRental.Domain.Vehicles;

public enum VehicleStatus
{
    Available = 0,
    OnRent = 1,
    InService = 2,
    Retired = 3
}

public enum Transmission
{
    Manual = 0,
    Automatic = 1
}

/// <summary>What happened to a car, in its logbook (rentals come from the Rentals table).</summary>
public enum VehicleLogKind
{
    Registered = 0,
    SentToService = 1,
    ReturnedFromService = 2,
    MovedBranch = 3,
    Retired = 4
}

/// <summary>VALUE OBJECT: one logbook line. Feeds the vehicle history timeline.</summary>
public sealed record VehicleLogEntry(VehicleLogKind Kind, DateTimeOffset At, int OdometerKm, BranchId BranchId, string? Note);

/// <summary>
/// AGGREGATE ROOT: one physical car in the fleet.
/// </summary>
/// <remarks>
/// Lifecycle: Available ⇄ OnRent (checkout / return), Available ⇄ InService, any but OnRent → Retired.
/// Invariants:
///  * Registration and VIN are valid (value objects) and unique (repository check + unique indexes).
///  * Model year is not in the future and at most 10 years old (rental fleet policy).
///  * The odometer never goes backwards.
///  * A car on rent cannot be serviced or retired; nothing leaves Retired.
/// A one-way return moves the car to the return branch (VehicleMovedBranch): fleet capacity follows.
/// </remarks>
public sealed class Vehicle : AggregateRoot<VehicleId>
{
    public const int MaxAgeYears = 10;

    private readonly List<VehicleLogEntry> _log = new();

    public Registration Registration { get; private set; } = null!;
    public Vin Vin { get; private set; } = null!;
    public string Make { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public VehicleCategory Category { get; private set; }
    public Transmission Transmission { get; private set; }
    public BranchId HomeBranchId { get; private set; }
    public BranchId CurrentBranchId { get; private set; }
    public int OdometerKm { get; private set; }
    public VehicleStatus Status { get; private set; }
    public RentalId? CurrentRentalId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<VehicleLogEntry> Log => _log.AsReadOnly();

    private Vehicle() { }   // EF Core

    public static Vehicle Register(
        Registration registration, Vin vin, string make, string model, int year,
        VehicleCategory category, Transmission transmission, BranchId homeBranchId, int odometerKm, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(make) || make.Trim().Length > 40)
            throw new DomainException("vehicle.make_required", "Make is required (max 40 characters).");
        if (string.IsNullOrWhiteSpace(model) || model.Trim().Length > 40)
            throw new DomainException("vehicle.model_required", "Model is required (max 40 characters).");
        if (year > now.Year)
            throw new DomainException("vehicle.invalid_year", "The model year cannot be in the future.");
        if (year < now.Year - MaxAgeYears)
            throw new DomainException("vehicle.too_old", $"Rental cars may be at most {MaxAgeYears} years old.");
        if (odometerKm < 0)
            throw new DomainException("vehicle.negative_odometer", "Odometer reading cannot be negative.");

        var vehicle = new Vehicle
        {
            Id = VehicleId.New(),
            Registration = registration,
            Vin = vin,
            Make = make.Trim(),
            Model = model.Trim(),
            Year = year,
            Category = category,
            Transmission = transmission,
            HomeBranchId = homeBranchId,
            CurrentBranchId = homeBranchId,
            OdometerKm = odometerKm,
            Status = VehicleStatus.Available,
            CreatedAt = now,
            UpdatedAt = now,
        };
        vehicle.Write(VehicleLogKind.Registered, now, null);
        vehicle.Raise(new VehicleRegistered(vehicle.Id, homeBranchId, category, now));
        return vehicle;
    }

    /// <summary>Keys handed over (called by Rental.CheckOut).</summary>
    public void AssignToRental(RentalId rentalId, int odometerKm, DateTimeOffset now)
    {
        EnsureNotRetired();
        if (Status != VehicleStatus.Available)
            throw new DomainException("vehicle.not_available", $"{Registration} is {Status}, not available.");
        MoveOdometer(odometerKm);

        Status = VehicleStatus.OnRent;
        CurrentRentalId = rentalId;
        UpdatedAt = now;
    }

    /// <summary>The car is back. A one-way return leaves it at the return branch.</summary>
    public void ReturnFromRental(int odometerKm, BranchId returnBranchId, DateTimeOffset now)
    {
        if (Status != VehicleStatus.OnRent)
            throw new DomainException("vehicle.not_on_rent", $"{Registration} is not on rent.");
        MoveOdometer(odometerKm);

        Status = VehicleStatus.Available;
        CurrentRentalId = null;
        UpdatedAt = now;

        if (returnBranchId != CurrentBranchId)
        {
            var from = CurrentBranchId;
            CurrentBranchId = returnBranchId;
            Write(VehicleLogKind.MovedBranch, now, null);
            Raise(new VehicleMovedBranch(Id, from, returnBranchId, Category, now));
        }
    }

    public void SendToService(string? reason, DateTimeOffset now)
    {
        EnsureNotRetired();
        EnsureNotOnRent("serviced");
        if (Status != VehicleStatus.Available)
            throw new DomainException("vehicle.not_available", $"{Registration} is already {Status}.");
        var why = RequireReason(reason);

        Status = VehicleStatus.InService;
        UpdatedAt = now;
        Write(VehicleLogKind.SentToService, now, why);
    }

    public void ReturnFromService(int odometerKm, DateTimeOffset now)
    {
        EnsureNotRetired();
        if (Status != VehicleStatus.InService)
            throw new DomainException("vehicle.not_in_service", $"{Registration} is not in service.");
        MoveOdometer(odometerKm);

        Status = VehicleStatus.Available;
        UpdatedAt = now;
        Write(VehicleLogKind.ReturnedFromService, now, null);
    }

    public void Retire(string? reason, DateTimeOffset now)
    {
        EnsureNotRetired();
        EnsureNotOnRent("retired");
        var why = RequireReason(reason);

        Status = VehicleStatus.Retired;
        UpdatedAt = now;
        Write(VehicleLogKind.Retired, now, why);
        Raise(new VehicleRetired(Id, CurrentBranchId, Category, why, now));
    }

    private void MoveOdometer(int odometerKm)
    {
        if (odometerKm < OdometerKm)
            throw new DomainException("vehicle.odometer_decreased",
                $"Odometer cannot go backwards: {Registration} is at {OdometerKm} km, {odometerKm} km was entered.");
        OdometerKm = odometerKm;
    }

    private void EnsureNotRetired()
    {
        if (Status == VehicleStatus.Retired)
            throw new DomainException("vehicle.retired", $"{Registration} is retired.");
    }

    private void EnsureNotOnRent(string action)
    {
        if (Status == VehicleStatus.OnRent)
            throw new DomainException("vehicle.on_rent", $"{Registration} is on rent and cannot be {action}.");
    }

    private static string RequireReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 200
            ? throw new DomainException("vehicle.reason_required", "A reason is required (max 200 characters).")
            : reason.Trim();

    private void Write(VehicleLogKind kind, DateTimeOffset at, string? note) =>
        _log.Add(new VehicleLogEntry(kind, at, OdometerKm, CurrentBranchId, note));
}

public sealed record VehicleRegistered(VehicleId VehicleId, BranchId BranchId, VehicleCategory Category, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record VehicleRetired(VehicleId VehicleId, BranchId BranchId, VehicleCategory Category, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record VehicleMovedBranch(VehicleId VehicleId, BranchId FromBranchId, BranchId ToBranchId, VehicleCategory Category, DateTimeOffset OccurredAt) : IDomainEvent;
