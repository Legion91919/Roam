namespace CarRental.Domain.ValueObjects;

/// <summary>Customers book a CATEGORY ("an SUV"), not a specific car. The car is assigned at pickup.</summary>
public enum VehicleCategory
{
    Economy = 0,
    Compact = 1,
    Suv = 2,
    Luxury = 3
}

/// <summary>Optional add-ons. [Flags] lets one column store any combination.</summary>
[Flags]
public enum Extras
{
    None = 0,
    Gps = 1,
    ChildSeat = 2
}
