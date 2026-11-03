using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.ValueObjects;

namespace CarRental.Domain.Inventory;

/// <summary>
/// DOMAIN SERVICE: reserve or release one car per occupied day across several inventory rows.
/// Lives in the Domain because "a booking needs a car on EVERY day of the period, at the pickup
/// branch, in the booked category" is a business rule, not an application detail.
/// </summary>
// SOLID (S): one job, mapping a period to the inventory days it consumes.
public static class InventoryAllocation
{
    public static void Reserve(BranchId branchId, VehicleCategory category, RentalPeriod period, IReadOnlyCollection<CategoryInventory> days)
    {
        foreach (var day in ForEachOccupiedDay(branchId, category, period, days))
            day.Reserve();
    }

    public static void Release(BranchId branchId, VehicleCategory category, RentalPeriod period, IReadOnlyCollection<CategoryInventory> days)
    {
        foreach (var day in ForEachOccupiedDay(branchId, category, period, days))
            day.Release();
    }

    private static List<CategoryInventory> ForEachOccupiedDay(
        BranchId branchId, VehicleCategory category, RentalPeriod period, IReadOnlyCollection<CategoryInventory> days)
    {
        if (days.Any(d => d.BranchId != branchId || d.Category != category))
            throw new InvalidOperationException("Inventory rows for a different branch or category were supplied.");

        var byDate = days.ToDictionary(d => d.Date);

        // Validate every day first, then mutate: no half-reserved state even in memory.
        var required = period.OccupiedDates().ToList();
        var missing = required.FirstOrDefault(date => !byDate.ContainsKey(date));
        if (missing != default)
            throw new CapacityExceededException("inventory.not_offered", $"{category} is not offered at this branch on {missing:yyyy-MM-dd}.");

        return required.Select(date => byDate[date]).ToList();
    }
}
