using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;

namespace CarRental.Domain.Branches;

public enum BranchType
{
    Airport = 0,
    City = 1
}

/// <summary>A rental location: an airport terminal desk or a city branch. Reference data.</summary>
public sealed class Branch : AggregateRoot<BranchId>
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public BranchType Type { get; private set; }
    public string Address { get; private set; } = string.Empty;

    private Branch() { }

    public static Branch Create(BranchId id, string code, string name, string city, BranchType type, string address)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 8)
            throw new DomainException("branch.invalid_code", "Branch code is required (max 8 characters).");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("branch.name_required", "Branch name is required.");
        if (string.IsNullOrWhiteSpace(city)) throw new DomainException("branch.city_required", "Branch city is required.");

        return new Branch
        {
            Id = id,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            City = city.Trim(),
            Type = type,
            Address = address.Trim(),
        };
    }
}
