using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.ValueObjects;

namespace CarRental.Domain.Inventory;

/// <summary>
/// AGGREGATE: how many cars of one category one branch can hand out on one day.
/// One row per (Branch, Category, Date).
/// </summary>
/// <remarks>
/// THIS IS THE OVERBOOKING GUARD.
///
/// The naive approach is "count overlapping reservations, then insert if there is room". Under
/// load, two requests can both count "1 car left" and both insert (a check-then-act race).
///
/// Instead, each day's capacity is a small aggregate with its own invariant
/// (Reserved never exceeds Capacity). Booking a 3-day rental calls <see cref="Reserve"/> on three
/// rows. In Infrastructure each row has a SQL Server ROWVERSION concurrency token, so if two
/// requests change the same row at the same time, the second SaveChanges matches 0 rows, EF throws
/// DbUpdateConcurrencyException, and the whole transaction (reservation insert included) rolls
/// back. The losing request is retried with fresh data and then either succeeds or gets a clean
/// "sold out" (409). The database is the final arbiter; no in-memory lock is needed, so this works
/// across many API instances.
/// </remarks>
public sealed class CategoryInventory : AggregateRoot<InventoryId>
{
    public BranchId BranchId { get; private set; }
    public VehicleCategory Category { get; private set; }
    public DateOnly Date { get; private set; }
    public int Capacity { get; private set; }
    public int Reserved { get; private set; }

    public int Available => Capacity - Reserved;

    private CategoryInventory() { }

    public static CategoryInventory Create(BranchId branchId, VehicleCategory category, DateOnly date, int capacity)
    {
        if (capacity < 0) throw new DomainException("inventory.negative_capacity", "Capacity cannot be negative.");

        return new CategoryInventory
        {
            Id = InventoryId.New(),
            BranchId = branchId,
            Category = category,
            Date = date,
            Capacity = capacity,
        };
    }

    /// <summary>Take one car for this day. Invariant: Reserved &lt;= Capacity.</summary>
    public void Reserve()
    {
        if (Reserved >= Capacity)
            throw new CapacityExceededException("inventory.sold_out", $"No {Category} cars are left at this branch on {Date:yyyy-MM-dd}.");

        Reserved++;
    }

    /// <summary>Give a car back (reservation cancelled).</summary>
    public void Release()
    {
        if (Reserved == 0)
            throw new DomainException("inventory.nothing_to_release", "There are no reservations to release for this day.");

        Reserved--;
    }

    /// <summary>Fleet planning: capacity can never drop below what is already promised to customers.</summary>
    public void ChangeCapacity(int capacity)
    {
        if (capacity < Reserved)
            throw new DomainException("inventory.capacity_below_reserved", $"Capacity cannot be lower than the {Reserved} cars already reserved.");

        Capacity = capacity;
    }
}
