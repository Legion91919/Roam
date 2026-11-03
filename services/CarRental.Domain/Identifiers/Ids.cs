namespace CarRental.Domain.Identifiers;

// ---------------------------------------------------------------------------------------------
// STRONGLY TYPED IDs
//
// Why not plain Guid? Because the compiler cannot stop this bug:
//     Reserve(customerId, reservationId);   // arguments swapped, both are Guid, compiles fine
// With a distinct type per ID, swapping them is a compile error.
//
// "readonly record struct" gives value equality, immutability and no heap allocation.
// EF Core stores each as a uniqueidentifier through a ValueConverter (Infrastructure), so the
// database never knows these types exist.
// ---------------------------------------------------------------------------------------------

public readonly record struct ReservationId(Guid Value)
{
    public static ReservationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct RentalId(Guid Value)
{
    public static RentalId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct CustomerId(Guid Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct BranchId(Guid Value)
{
    public static BranchId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct InventoryId(Guid Value)
{
    public static InventoryId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct PaymentId(Guid Value)
{
    public static PaymentId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct VehicleId(Guid Value)
{
    public static VehicleId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct InspectionId(Guid Value)
{
    public static InspectionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct AttachmentId(Guid Value)
{
    public static AttachmentId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
