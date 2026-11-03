using CarRental.Domain.Common;

namespace CarRental.Domain.Inventory;

/// <summary>
/// DOMAIN SERVICE: the fleet drives inventory. One more car of a category at a branch means one
/// more bookable car on every future day; one car fewer means one fewer.
/// </summary>
public static class FleetCapacity
{
    /// <summary>A car joined the branch (registered, or arrived by a one-way return).</summary>
    public static void AddCar(IEnumerable<CategoryInventory> futureDays)
    {
        foreach (var day in futureDays)
            day.ChangeCapacity(day.Capacity + 1);
    }

    /// <summary>
    /// A car left the fleet for good (retired). Strict: fails with inventory.capacity_below_reserved
    /// if any future day already has every car promised, because a booking would then have no car.
    /// </summary>
    public static void RemoveCar(IEnumerable<CategoryInventory> futureDays)
    {
        var days = futureDays.ToList();
        var full = days.Where(d => d.Capacity - 1 < d.Reserved).OrderBy(d => d.Date).FirstOrDefault();
        if (full is not null)
            throw new DomainException("inventory.capacity_below_reserved",
                $"Every {full.Category} car at this branch is booked on {full.Date:yyyy-MM-dd}: retiring this car would leave a booking without a car.");

        foreach (var day in days)
            day.ChangeCapacity(day.Capacity - 1);
    }

    /// <summary>
    /// A car left the branch on a one-way return. The car is physically gone, so the return cannot be
    /// refused: capacity drops on every future day where that is possible. Days that are already fully
    /// booked keep their capacity (the counter must move a car back) and are returned for reporting.
    /// </summary>
    public static IReadOnlyList<DateOnly> RemoveCarWherePossible(IEnumerable<CategoryInventory> futureDays)
    {
        var shortDays = new List<DateOnly>();
        foreach (var day in futureDays)
        {
            if (day.Capacity - 1 < day.Reserved) shortDays.Add(day.Date);
            else day.ChangeCapacity(day.Capacity - 1);
        }
        return shortDays;
    }
}
